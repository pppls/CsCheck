﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

// TODO:
// NaN, Infinity
// IEnumerable may be a bad ides for distribution as can't repeat, change to how frequency does it.

namespace CsCheck
{
    public class Size
    {
        public ulong I { get; }
        public Size[] Next { get; }
        public Size(ulong i, Size[] next)
        {
            I = i;
            Next = next;
        }
        internal bool IsLessThan(Size s) => I != s.I ? I < s.I : IsLessThan(Next, s.Next);
        static bool IsLessThan(IEnumerable<Size> a, IEnumerable<Size> b)
        {
            if (a is null || b is null) return b is object;
            ulong ta = a.Aggregate(0UL, (s, i) => s + i.I);
            ulong tb = b.Aggregate(0UL, (s, i) => s + i.I);
            return ta == tb ? IsLessThan(a.SelectMany(i => i.Next), b.SelectMany(i => i.Next)) // TODO: one of the next could be null
                : ta < tb;
        }
    }

    public abstract class Gen<T> : IEnumerable<T>
    {
        public abstract (T, Size) Generate(PCG pcg);
        IEnumerable<T> ToEnumerable()
        {
            var pcg = PCG.ThreadPCG;
            while (true) yield return Generate(pcg).Item1;
        }
        public IEnumerator<T> GetEnumerator() => ToEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ToEnumerable().GetEnumerator();
    }

    class GenF<T> : Gen<T>
    {
        readonly Func<PCG, (T, Size)> generate;
        public GenF(Func<PCG, (T, Size)> generate) => this.generate = generate;
        public override (T, Size) Generate(PCG pcg) => generate(pcg);
    }

    public static class Gen
    {
        public static Gen<T> Create<T>(Func<PCG, (T, Size)> gen) => new GenF<T>(gen);
        public static Gen<R> Select<T, R>(this Gen<T> gen, Func<T, R> selector) => new GenF<R>(pcg =>
        {
            var (v, s) = gen.Generate(pcg);
            return (selector(v), s);
        });
        public static Gen<R> Select<T1, T2, R>(this Gen<T1> gen1, Gen<T2> gen2,
            Func<T1, T2, R> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            return (selector(v1, v2), new Size(0UL, new[] { s1, s2 }));
        });
        public static Gen<R> Select<T1, T2, T3, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3,
            Func<T1, T2, T3, R> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            return (selector(v1, v2, v3), new Size(0UL, new[] { s1, s2, s3 }));
        });
        public static Gen<R> Select<T1, T2, T3, T4, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Func<T1, T2, T3, T4, R> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            return (selector(v1, v2, v3, v4), new Size(0UL, new[] { s1, s2, s3, s4 }));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Func<T1, T2, T3, T4, T5, R> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5), new Size(0UL, new[] { s1, s2, s3, s4, s5 }));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Gen<T6> gen6, Func<T1, T2, T3, T4, T5, T6, R> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            var (v6, s6) = gen6.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5, v6), new Size(0UL, new[] { s1, s2, s3, s4, s5, s6 }));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, T7, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Gen<T6> gen6, Gen<T7> gen7, Func<T1, T2, T3, T4, T5, T6, T7, R> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            var (v6, s6) = gen6.Generate(pcg);
            var (v7, s7) = gen7.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5, v6, v7), new Size(0UL, new[] { s1, s2, s3, s4, s5, s6, s7 }));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, T7, T8, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Gen<T6> gen6, Gen<T7> gen7, Gen<T8> gen8, Func<T1, T2, T3, T4, T5, T6, T7, T8, R> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            var (v6, s6) = gen6.Generate(pcg);
            var (v7, s7) = gen7.Generate(pcg);
            var (v8, s8) = gen8.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5, v6, v7, v8), new Size(0UL, new[] { s1, s2, s3, s4, s5, s6, s7, s8 }));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Gen<T6> gen6, Gen<T7> gen7, Gen<T8> gen8, Gen<T9> gen9,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            var (v6, s6) = gen6.Generate(pcg);
            var (v7, s7) = gen7.Generate(pcg);
            var (v8, s8) = gen8.Generate(pcg);
            var (v9, s9) = gen9.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5, v6, v7, v8, v9), new Size(0UL, new[] { s1, s2, s3, s4, s5, s6, s7, s8, s9 }));
        });
        public static Gen<(T0 V0, T1 V1)> Select<T0, T1>(this Gen<T0> gen0, Gen<T1> gen1) => new GenF<(T0, T1)>(pcg =>
        {
            var (v0, s0) = gen0.Generate(pcg);
            var (v1, s1) = gen1.Generate(pcg);
            return ((v0, v1), new Size(0UL, new[] { s0, s1 }));
        });
        public static Gen<(T0 V0, T1 V1, T2 V2)> Select<T0, T1, T2>(this Gen<T0> gen0, Gen<T1> gen1, Gen<T2> gen2)
            => new GenF<(T0, T1, T2)>(pcg =>
        {
            var (v0, s0) = gen0.Generate(pcg);
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            return ((v0, v1, v2), new Size(0UL, new[] { s0, s1, s2 }));
        });
        public static Gen<R> SelectMany<T, R>(this Gen<T> gen, Func<T, Gen<R>> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen.Generate(pcg);
            var genR = selector(v1);
            var (vR, sR) = genR.Generate(pcg);
            return (vR, new Size(s1.I, new[] { sR }));
        });
        public static Gen<R> SelectMany<T1, T2, R>(this Gen<T1> gen1, Gen<T2> gen2, Func<T1, T2, Gen<R>> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var genR = selector(v1, v2);
            var (vR, sR) = genR.Generate(pcg);
            return (vR, new Size(s1.I + s2.I, new[] { sR }));
        });
        public static Gen<R> SelectMany<T1, T2, T3, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3,
            Func<T1, T2, T3, Gen<R>> selector) => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var genR = selector(v1, v2, v3);
            var (vR, sR) = genR.Generate(pcg);
            return (vR, new Size(s1.I + s2.I + s3.I, new[] { sR }));
        });
        public static Gen<R> SelectMany<T1, T2, R>(this Gen<T1> gen1, Func<T1, Gen<T2>> genSelector, Func<T1, T2, R> resultSelector)
            => new GenF<R>(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var gen2 = genSelector(v1);
            var (v2, s2) = gen2.Generate(pcg);
            return (resultSelector(v1, v2), new Size(s1.I, new[] { s2 }));
        });
        public static Gen<T[]> Array<T>(this Gen<T> gen, Gen<int> genLength) => new GenF<T[]>(pcg =>
        {
            var (l, sl) = genLength.Generate(pcg);
            var vs = new T[l];
            var ss = new Size[l];
            for (int i = 0; i < vs.Length; i++)
            {
                var (v, s) = gen.Generate(pcg);
                vs[i] = v;
                ss[i] = s;
            }
            return (vs, new Size(sl.I, ss));
        });
        public static Gen<T[]> Array<T>(this Gen<T> gen, int start, int finish) => Array(gen, Int[start, finish]);
        public static Gen<T[]> Array<T>(this Gen<T> gen, int length) => Array(gen, Const(length));
        public static Gen<List<T>> List<T>(this Gen<T> gen, Gen<int> genLength) => new GenF<List<T>>(pcg =>
        {
            var (l, sl) = genLength.Generate(pcg);
            var vs = new List<T>(l);
            var ss = new Size[l];
            for (int i = 0; i < l; i++)
            {
                var (v, s) = gen.Generate(pcg);
                vs[i] = v;
                ss[i] = s;
            }
            return (vs, new Size(sl.I, ss));
        });
        public static Gen<List<T>> List<T>(this Gen<T> gen, int start, int finish) => List(gen, Int[start, finish]);
        public static Gen<List<T>> List<I, T>(this Gen<T> gen, int length) => List(gen, Const(length));
        static readonly Size zero = new Size(0UL, null);
        public static Gen<T> Const<T>(T value) => new GenF<T>(_ => (value, zero));
        public static Gen<T> OneOf<T>(params T[] ts) => Int[0, ts.Length - 1].Select(i => ts[i]);
        public static Gen<T> OneOf<T>(params Gen<T>[] gens) => Int[0, gens.Length - 1].SelectMany(i => gens[i]);
        public static Gen<T> Frequency<T>(params (int, Gen<T>)[] gens)
        {
            gens = ((int, Gen<T>)[])gens.Clone();
            int total = 0;
            for (int i = 0; i < gens.Length; i++)
            {
                total += gens[i].Item1;
                gens[i].Item1 = total;
            }
            return Int[1, total].SelectMany(c =>
            {
                for (int i = 0; i < gens.Length; i++)
                    if (c <= gens[i].Item1)
                        return gens[i].Item2;
                return null;
            });
        }
        public static readonly GenBool Bool = new GenBool();
        public static readonly GenSByte SByte = new GenSByte();
        public static readonly GenByte Byte = new GenByte();
        public static readonly GenShort Short = new GenShort();
        public static readonly GenUShort UShort = new GenUShort();
        public static readonly GenInt Int = new GenInt();
        public static readonly GenUInt UInt = new GenUInt();
        public static readonly GenLong Long = new GenLong();
        public static readonly GenULong ULong = new GenULong();
        public static readonly GenSingle Single = new GenSingle();
        public static readonly GenDouble Double = new GenDouble();
        public static readonly GenDecimal Decimal = new GenDecimal();
        public static readonly GenChar Char = new GenChar();
        public static readonly GenString String = new GenString();
    }

    public class GenBool : Gen<bool>
    {
        public override (bool, Size) Generate(PCG pcg)
        {
            uint i = pcg.Next();
            return ((i & 1u) == 1u, new Size(i, null));
        }
    }

    public class GenSByte : Gen<sbyte>
    {
        static ulong Zigzag(sbyte i) => (ulong)((i << 1) ^ (i >> 7));
        public override (sbyte, Size) Generate(PCG pcg)
        {
            sbyte i = (sbyte)(pcg.Next() & 255u);
            return (i, new Size(Zigzag(i), null));
        }
        public Gen<sbyte> this[sbyte start, sbyte finish]
        {
            get
            {
                uint l = (uint)(finish - start) + 1u;
                return new GenF<sbyte>(pcg =>
                {
                    sbyte i = (sbyte)(start + pcg.Next(l));
                    return (i, new Size(Zigzag(i), null));
                });
            }
        }
    }

    public class GenByte : Gen<byte>
    {
        public override (byte, Size) Generate(PCG pcg)
        {
            byte i = (byte)(pcg.Next() & 255u);
            return (i, new Size(i, null));
        }
        public Gen<byte> this[byte start, byte finish]
        {
            get
            {
                uint s = start;
                uint l = finish - s + 1u;
                return new GenF<byte>(pcg =>
                {
                    byte i = (byte)(s + pcg.Next(l));
                    return (i, new Size(i, null));
                });
            }
        }
    }

    public class GenShort : Gen<short>
    {
        static ulong Zigzag(short i) => (ulong)((i << 1) ^ (i >> 15));
        public override (short, Size) Generate(PCG pcg)
        {
            short i = (short)(pcg.Next() & 65535u);
            return (i, new Size(Zigzag(i), null));
        }
        public Gen<short> this[short start, short finish]
        {
            get
            {
                uint l = (uint)(finish - start) + 1u;
                return new GenF<short>(pcg =>
                {
                    short i = (short)(start + pcg.Next(l));
                    return (i, new Size(Zigzag(i), null));
                });
            }
        }
    }

    public class GenUShort : Gen<ushort>
    {
        public override (ushort, Size) Generate(PCG pcg)
        {
            ushort i = (ushort)(pcg.Next() & 65535u);
            return (i, new Size(i, null));
        }
        public Gen<ushort> this[ushort start, ushort finish]
        {
            get
            {
                uint l = (uint)(finish - start) + 1u;
                return new GenF<ushort>(pcg =>
                {
                    ushort i = (ushort)(start + pcg.Next(l));
                    return (i, new Size(i, null));
                });
            }
        }
    }

    public class GenInt : Gen<int>
    {
        static uint Zigzag(int i) => (uint)((i << 1) ^ (i >> 31));
        public override (int, Size) Generate(PCG pcg)
        {
            int i = (int)pcg.Next();
            return (i, new Size(Zigzag(i), null));
        }
        public Gen<int> this[int start, int finish]
        {
            get
            {
                uint l = (uint)(finish - start) + 1u;
                return new GenF<int>(pcg =>
                {
                    int i = (int)(start + pcg.Next(l));
                    return (i, new Size(Zigzag(i), null));
                });
            }
        }
    }

    public class GenUInt : Gen<uint>
    {
        public override (uint, Size) Generate(PCG pcg)
        {
            uint i = pcg.Next();
            return (i, new Size(i, null));
        }
        public Gen<uint> this[uint start, uint finish]
        {
            get
            {
                uint l = finish - start + 1u;
                return new GenF<uint>(pcg =>
                {
                    uint i = start + pcg.Next(l);
                    return (i, new Size(i, null));
                });
            }
        }
    }

    public class GenLong : Gen<long>
    {
        static ulong Zigzag(long i) => (ulong)((i << 1) ^ (i >> 63));

        public override (long, Size) Generate(PCG pcg)
        {
            long i = (long)pcg.Next64();
            return (i, new Size(Zigzag(i), null));
        }
        public Gen<long> this[long start, long finish]
        {
            get
            {

                ulong l = (ulong)(finish - start) + 1ul;
                return new GenF<long>(pcg =>
                {
                    long i = start + (long)pcg.Next64(l);
                    return (i, new Size(Zigzag(i), null));
                });
            }
        }
    }

    public class GenULong : Gen<ulong>
    {
        public override (ulong, Size) Generate(PCG pcg)
        {
            ulong i = pcg.Next64();
            return (i, new Size(i, null));
        }
        public Gen<ulong> this[ulong start, ulong finish]
        {
            get
            {
                ulong l = finish - start + 1ul;
                return new GenF<ulong>(pcg =>
                {
                    ulong i = start + pcg.Next64(l);
                    return (i, new Size(i, null));
                });
            }
        }
    }

    public class GenSingle : Gen<float>
    {
        [StructLayout(LayoutKind.Explicit)]
        struct Converter
        {
            [FieldOffset(0)] public int I;
            [FieldOffset(0)] public float F;
        }
        public override (float, Size) Generate(PCG pcg)
        {
            ulong i = pcg.Next() >> 9;
            return (new Converter { I = (int)i | 0x3F800000 }.F - 1f
                    , new Size(i, null));
        }
        public Gen<float> this[float start, float finish]
        {
            get
            {
                finish -= start;
                start -= finish;
                return new GenF<float>(pcg =>
                {
                    ulong i = pcg.Next() >> 9;
                    return (new Converter { I = (int)i | 0x3F800000 }.F * finish + start
                            , new Size(i, null));
                });
            }
        }
    }

    public class GenDouble : Gen<double>
    {
        public override (double, Size) Generate(PCG pcg)
        {
            ulong i = pcg.Next64() >> 12;
            return (BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000L) - 1.0
                    , new Size(i, null));
        }
        public Gen<double> this[double start, double finish]
        {
            get
            {
                finish -= start;
                start -= finish;
                return new GenF<double>(pcg =>
                {
                    ulong i = pcg.Next64() >> 12;
                    return (BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000L) * finish + start
                            , new Size(i, null));
                });
            }
        }
    }

    public class GenDecimal : Gen<decimal>
    {
        public override (decimal, Size) Generate(PCG pcg)
        {
            var i = pcg.Next64() >> 12;
            return ((decimal)BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000L) - 1M
                    , new Size(i, null));
        }
        public Gen<decimal> this[decimal start, decimal finish]
        {
            get
            {
                finish -= start;
                start -= finish;
                return new GenF<decimal>(pcg =>
                {
                    var i = pcg.Next64() >> 12;
                    return ((decimal)BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000L) * finish + start
                            , new Size(i, null));
                });
            }
        }
    }

    public class GenChar : Gen<char>
    {
        public override (char, Size) Generate(PCG pcg)
        {
            var i = pcg.Next() & 127u;
            return ((char)i, new Size(i, null));
        }
        public Gen<char> this[char start, char finish]
        {
            get
            {
                uint s = start;
                uint l = finish + 1u - s;
                return new GenF<char>(pcg =>
                {
                    var i = pcg.Next(l);
                    return ((char)(s + i), new Size(i, null));
                });
            }
        }
        public Gen<char> this[string chars]
        {
            get
            {
                char[] charArray = chars.ToArray();
                return new GenF<char>(pcg =>
                {
                    var i = pcg.Next((uint)charArray.Length);
                    return (charArray[i], new Size(i, null));
                });
            }
        }
    }

    public class GenString : Gen<string>
    {
        static readonly Gen<string> d = Gen.Char.Array(0, 127).Select(i => new string(i));
        public override (string, Size) Generate(PCG pcg)
        {
            return d.Generate(pcg);
        }
        public Gen<string> this[int start, int finish]
        {
            get
            {
                return Gen.Char.Array(start, finish).Select(i => new string(i));
            }
        }
        public Gen<string> this[Gen<char> gen, int start, int finish]
        {
            get
            {
                return gen.Array(start, finish).Select(i => new string(i));
            }
        }
    }
}