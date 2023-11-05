﻿namespace Tests;

using System;
using System.Diagnostics;

public static class MathX
{
    public static double TwoSum(double a, double b, out double lo)
    {
        var hi = a + b;
        lo = hi - b;
        lo = lo - hi + b + (a - lo);
        return hi;
    }

    public static double FastTwoSum(double a, double b, out double lo)
    {
        Debug.Assert(Math.Abs(a) >= Math.Abs(b));
        var hi = a + b;
        lo = a - hi + b;
        return hi;
    }

    public static double TwoSub(double a, double b, out double lo)
    {
        var hi = a - b;
        lo = a - hi;
        lo = a - (hi + lo) + (lo - b);
        return hi;
    }

    public static double TwoMul(double a, double b, out double lo)
    {
        var prod = a * b;
        lo = Math.FusedMultiplyAdd(a, b, -prod);
        return prod;
    }

    /// <summary>Kahan summation</summary>
    public static double KSum(this double[] values)
    {
        var sum = 0.0;
        var c = 0.0;
        foreach (var value in values)
            sum = FastTwoSum(sum, value + c, out c);
        return sum;
    }

    /// <summary>Neumaier summation</summary>
    public static double NSum(this double[] values)
    {
        var sum = 0.0;
        var c = 0.0;
        for (int i = 0; i < values.Length; i++)
        {
            sum = TwoSum(sum, values[i], out var ci);
            c += ci;
        }
        return sum + c;
    }

    /// <summary>Shewchuk summation</summary>
    public static double FSum(this double[] values)
    {
        Span<double> partials = stackalloc double[16];
        var hi = TwoSum(values[0], values[1], out var lo);
        int count = 0;
        for (int i = 2; i < values.Length; i++)
        {
            var v = TwoSum(values[i], lo, out lo);
            int c = 0;
            for (int j = 0; j < count; j++)
            {
                v = TwoSum(v, partials[j], out var p);
                if (p != 0.0)
                    partials[c++] = p;
            }
            hi = TwoSum(hi, v, out v);
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

        if (count != 0)
        {
            if (lo == 0) // lo has a good chance of being zero
            {
                lo = partials[0];
                if (count == 1) return lo + hi;
                partials = partials[1..count];
            }
            else
                partials = partials[..count];
            Compress(ref lo, ref partials, ref hi);
            foreach (var p in partials)
                lo += p;
        }
        return lo + hi;
    }

    static void Compress(ref double lo, ref Span<double> partials, ref double hi)
    {
        double q;
        hi = TwoSum(hi, partials[^1], out var Q);
        var bottom = partials.Length;
        for (int i = partials.Length - 2; i >= 0; i--)
        {
            Q = TwoSum(Q, partials[i], out q);
            if (q != 0.0)
            {
                partials[--bottom] = Q;
                Q = q;
            }
        }
        lo = TwoSum(Q, lo, out q);
        if (q != 0.0)
        {
            partials[--bottom] = lo;
            lo = q;
        }
        if (bottom == partials.Length) { partials = []; return; }
        Q = TwoSum(partials[bottom], lo, out lo);
        var top = 0;
        for (int i = bottom + 1; i < partials.Length; i++)
        {
            Q = TwoSum(partials[i], Q, out q);
            if (q != 0.0) partials[top++] = q;
        }
        hi = TwoSum(hi, Q, out q);
        if (q != 0.0) partials[top++] = q;
        partials = partials[..top];
    }

    static void Renormalise(ref Span<double> e)
    {
        var Q = e[^1];
        var bottom = e.Length - 1;
        for (int i = e.Length - 2; i >= 0; i--)
        {
            Q = FastTwoSum(Q, e[i], out var q);
            if (q != 0.0)
            {
                e[bottom--] = Q;
                Q = q;
            }
        }
        e[bottom] = Q;
        var top = 0;
        e[0] = Q;
        for (int i = bottom + 1; i < e.Length; i++)
        {
            Q = TwoSum(e[i], e[top], out var q);
            e[top] = Q;
            if (q != 0.0)
            {
                var l = top++ - 1;
                while (l >= 0)
                {
                    var c2 = TwoSum(e[l + 1], e[l], out var d2);
                    if (d2 == 0.0)
                    {
                        e[l--] = c2;
                        top--;
                    }
                    else
                        break;
                }
                e[top] = q;
            }
        }
        e = e[..(top + 1)];
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
}