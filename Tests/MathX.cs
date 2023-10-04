﻿namespace Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;

public static class MathX
{
    public static long Mantissa(double d, out int exponent)
    {
        var bits = BitConverter.DoubleToInt64Bits(d);
        exponent = (int)((bits >> 52) & 0x7FFL);
        var mantissa = bits & 0xF_FFFF_FFFF_FFFF;
        if (exponent == 0)
        {
            exponent = -1074;
        }
        else
        {
            exponent -= 1075;
            mantissa |= 1L << 52;
        }
        return (bits & (1L << 63)) == 0 ? mantissa : -mantissa;
    }

    public static (double hi, double lo) TwoSum(double a, double b)
    {
        var hi = a + b;
        var a2 = hi - b;
        return (hi, a2 - hi + b + (a - a2));
    }

    public static (double hi, double lo) FastTwoSum(double a, double b)
    {
        Debug.Assert(Math.Abs(a) >= Math.Abs(b));
        var hi = a + b;
        return (hi, a - hi + b);
    }

    public static double KSum(this double[] values)
    {
        var sum = 0.0;
        var err = 0.0;
        for (int i = 0; i < values.Length; i++)
        {
            (sum, var e) = TwoSum(sum, values[i]);
            err += e;
        }
        return sum + err;
    }

    public static double FSum(this double[] values)
    {
        Span<double> partials = stackalloc double[16];
        int count = 0;
        var hi = 0.0;
        var lo = 0.0;
        for (int i = 0; i < values.Length; i++)
        {
            (var v, lo) = TwoSum(values[i], lo);
            int c = 0;
            for (int j = 0; j < count; j++)
            {
                (v, var partial) = TwoSum(v, partials[j]);
                if (partial != 0.0)
                    partials[c++] = partial;
            }
            (hi, v) = TwoSum(hi, v);
            if (v != 0.0)
            {
                if (c == partials.Length)
                {
                    var newPartials = new double[partials.Length * 2];
                    partials.CopyTo(newPartials);
                    partials = newPartials;
                }
                partials[c++] = v;
            }
            count = c;
        }
        while (--count >= 0)
            lo += partials[count];
        return lo + hi;
    }

    public static double SSum(this double[] values)
    {
        if (values.Length == 0)
            return 0.0;
        values = (double[])values.Clone();
        Array.Sort(values, (x, y) => Math.Abs(x).CompareTo(Math.Abs(y)));
        var prev = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            var next = values[i];
            if (next == -prev)
            {
                values[i - 1] = 0;
                values[i] = 0;
                prev = 0;
            }
            else
            {
                prev = next;
            }
        }
        return values.FSum();
    }

    public static double LSum(IEnumerable<double> values)
    {
        var totalMantissa = 0L;
        var totalExponent = 0;
        foreach (var v in values)
        {
            var mantissa = Mantissa(v, out var exponent);
            if (totalExponent > exponent)
            {
                totalMantissa <<= totalExponent - exponent;
                totalExponent = exponent;
            }
            else
            {
                mantissa <<= exponent - totalExponent;
            }
            totalMantissa += mantissa;
        }
        return Math.ScaleB(totalMantissa, totalExponent);
    }
}