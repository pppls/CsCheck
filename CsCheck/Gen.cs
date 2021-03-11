﻿// Copyright 2021 Anthony Lloyd
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace CsCheck
{
    public class Size
    {
        public readonly static Size[] EmptyArray = new Size[0];
        public readonly static Size Zero = new(0UL);
        public readonly static Size Max = new(ulong.MaxValue);
        public readonly ulong I;
        public readonly Size[] Next;
        public Size(ulong i, Size[] next)
        {
            I = i;
            Next = next;
        }
        public Size(ulong i)
        {
            I = i;
            Next = EmptyArray;
        }
        public Size(params Size[] next)
        {
            Next = next;
        }
        static ulong Sum(Size[] a, int level)
        {
            ulong t = 0UL;
            if (level == 0)
            {
                foreach (var i in a) t += i.I + 1UL;
                return t;
            }
            level--;
            foreach (var i in a) t += Sum(i.Next, level);
            return t;
        }
        static bool IsLessThan(Size[] a, Size[] b, int level)
        {
            ulong ta = Sum(a, level);
            ulong tb = Sum(b, level);
            return ta < tb || (ta == tb && ta != 0UL && IsLessThan(a, b, level + 1));
        }
        public bool IsLessThan(Size s) => I < s.I || (I == s.I && IsLessThan(Next, s.Next, 0));
        public Size Append(Size s)
        {
            if (Next == EmptyArray) return new Size(I, new[] { s });
            var next = new Size[Next.Length];
            for (int i = 0; i < next.Length; i++)
                next[i] = Next[i].Append(s);
            return new Size(I, next);
        }
    }

    public abstract class Gen<T>
    {
        public abstract (T, Size) Generate(PCG pcg);
        public Gen<R> Cast<R>() => Gen.Create(pcg =>
        {
            var (o, s) = Generate(pcg);
            return (o is R t ? t : (R)Convert.ChangeType(o, typeof(R)), s);
        });
        public Gen<(string, Action<R>)> Operation<R>(Func<T, string> name, Action<T, R> f)
        {
            return Gen.Create<(string, Action<R>)>(pcg => {
                var (t, s) = Generate(pcg);
                return ((name(t), r => f(t, r)), s);
            });
        }
        public GenArray<T> Array => new(this);
        public GenEnumerable<T> Enumerable => new(this);
        public GenArray2D<T> Array2D => new(this);
        public GenList<T> List => new(this);
        public GenHashSet<T> HashSet => new(this);
        public GenArrayUnique<T> ArrayUnique => new(this);
    }

    public static class Gen
    {
        class GenFunc<T> : Gen<T>
        {
            readonly Func<PCG, (T, Size)> generate;
            internal GenFunc(Func<PCG, (T, Size)> generate) => this.generate = generate;
            public override (T, Size) Generate(PCG pcg) => generate(pcg);
        }
        public static Gen<T> Create<T>(Func<PCG, (T, Size)> gen) => new GenFunc<T>(gen);
        public static Gen<R> Select<T, R>(this Gen<T> gen, Func<T, R> selector) => Create(pcg =>
        {
            var (v, s) = gen.Generate(pcg);
            return (selector(v), s);
        });
        public static Gen<R> Select<T1, T2, R>(this Gen<T1> gen1, Gen<T2> gen2,
            Func<T1, T2, R> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            return (selector(v1, v2), new Size(s1, s2));
        });
        public static Gen<R> Select<T1, T2, T3, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3,
            Func<T1, T2, T3, R> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            return (selector(v1, v2, v3), new Size(s1, s2, s3));
        });
        public static Gen<R> Select<T1, T2, T3, T4, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Func<T1, T2, T3, T4, R> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            return (selector(v1, v2, v3, v4), new Size(s1, s2, s3, s4));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Func<T1, T2, T3, T4, T5, R> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5), new Size(s1, s2, s3, s4, s5));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Gen<T6> gen6, Func<T1, T2, T3, T4, T5, T6, R> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            var (v6, s6) = gen6.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5, v6), new Size(s1, s2, s3, s4, s5, s6));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, T7, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Gen<T6> gen6, Gen<T7> gen7, Func<T1, T2, T3, T4, T5, T6, T7, R> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            var (v6, s6) = gen6.Generate(pcg);
            var (v7, s7) = gen7.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5, v6, v7), new Size(s1, s2, s3, s4, s5, s6, s7));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, T7, T8, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Gen<T6> gen6, Gen<T7> gen7, Gen<T8> gen8, Func<T1, T2, T3, T4, T5, T6, T7, T8, R> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            var (v6, s6) = gen6.Generate(pcg);
            var (v7, s7) = gen7.Generate(pcg);
            var (v8, s8) = gen8.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5, v6, v7, v8), new Size(s1, s2, s3, s4, s5, s6, s7, s8));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, T7, T8, T9, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Gen<T6> gen6, Gen<T7> gen7, Gen<T8> gen8, Gen<T9> gen9,
            Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, R> selector) => Create(pcg =>
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
            return (selector(v1, v2, v3, v4, v5, v6, v7, v8, v9), new Size(s1, s2, s3, s4, s5, s6, s7, s8, s9));
        });
        public static Gen<R> Select<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3,
            Gen<T4> gen4, Gen<T5> gen5, Gen<T6> gen6, Gen<T7> gen7, Gen<T8> gen8, Gen<T9> gen9, Gen<T10> gen10,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, R> selector) => Create(pcg =>
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
            var (v10, s10) = gen10.Generate(pcg);
            return (selector(v1, v2, v3, v4, v5, v6, v7, v8, v9, v10), new Size(s1, s2, s3, s4, s5, s6, s7, s8, s9, s10));
        });
        public static Gen<(T0 V0, T1 V1)> Select<T0, T1>(this Gen<T0> gen0, Gen<T1> gen1) => Create(pcg =>
        {
            var (v0, s0) = gen0.Generate(pcg);
            var (v1, s1) = gen1.Generate(pcg);
            return ((v0, v1), new Size(s0, s1));
        });
        public static Gen<(T0 V0, T1 V1, T2 V2)> Select<T0, T1, T2>(this Gen<T0> gen0, Gen<T1> gen1, Gen<T2> gen2)
            => Create(pcg =>
        {
            var (v0, s0) = gen0.Generate(pcg);
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            return ((v0, v1, v2), new Size(s0, s1, s2));
        });
        public static Gen<(T0 V0, T1 V1, T2 V2, T3 V3)> Select<T0, T1, T2, T3>(this Gen<T0> gen0, Gen<T1> gen1,
            Gen<T2> gen2, Gen<T3> gen3) => Create(pcg =>
        {
            var (v0, s0) = gen0.Generate(pcg);
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            return ((v0, v1, v2, v3), new Size(s0, s1, s2, s3));
        });
        public static Gen<(T0 V0, T1 V1, T2 V2, T3 V3, T4 V4)> Select<T0, T1, T2, T3, T4>(this Gen<T0> gen0, Gen<T1> gen1,
            Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4) => Create(pcg =>
        {
            var (v0, s0) = gen0.Generate(pcg);
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            return ((v0, v1, v2, v3, v4), new Size(s0, s1, s2, s3, s4));
        });
        public static Gen<(T0 V0, T1 V1)> Select<T0, T1>(this Gen<T0> gen, Func<T0, Gen<T1>> selector) => Create(pcg =>
        {
            var (v1, s1) = gen.Generate(pcg);
            var genR = selector(v1);
            var (vR, sR) = genR.Generate(pcg);
            return ((v1, vR), s1.Append(sR));
        });
        public static Gen<(T0 V0, T1 V1, T2 V2)> Select<T0, T1, T2>(this Gen<T0> gen0, Gen<T1> gen1,
            Func<T0, T1, Gen<T2>> selector) => Create(pcg =>
        {
            var (v0, s0) = gen0.Generate(pcg);
            var (v1, s1) = gen1.Generate(pcg);
            var gen2 = selector(v0, v1);
            var (v2, s2) = gen2.Generate(pcg);
            return ((v0, v1, v2), new Size(s0.Append(s2), s1.Append(s2)));
        });
        public static Gen<(T0 V0, T1 V1, T2 V2, T3 V3)> Select<T0, T1, T2, T3>(this Gen<T0> gen0, Gen<T1> gen1, Gen<T2> gen2,
            Func<T0, T1, T2, Gen<T3>> selector) => Create(pcg =>
        {
            var (v0, s0) = gen0.Generate(pcg);
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var gen3 = selector(v0, v1, v2);
            var (v3, s3) = gen3.Generate(pcg);
            return ((v0, v1, v2, v3), new Size(s0.Append(s3), s1.Append(s3), s2.Append(s3)));
        });
        public static Gen<R> SelectMany<T, R>(this Gen<T> gen, Func<T, Gen<R>> selector) => Create(pcg =>
        {
            var (v1, s1) = gen.Generate(pcg);
            var genR = selector(v1);
            var (vR, sR) = genR.Generate(pcg);
            return (vR, s1.Append(sR));
        });
        public static Gen<R> SelectMany<T1, T2, R>(this Gen<T1> gen1, Gen<T2> gen2, Func<T1, T2, Gen<R>> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var genR = selector(v1, v2);
            var (vR, sR) = genR.Generate(pcg);
            return (vR, new Size(s1.Append(sR), s2.Append(sR)));
        });
        public static Gen<R> SelectMany<T1, T2, T3, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3,
            Func<T1, T2, T3, Gen<R>> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var genR = selector(v1, v2, v3);
            var (vR, sR) = genR.Generate(pcg);
            return (vR, new Size(s1.Append(sR), s2.Append(sR), s3.Append(sR)));
        });
        public static Gen<R> SelectMany<T1, T2, T3, T4, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Func<T1, T2, T3, T4, Gen<R>> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var genR = selector(v1, v2, v3, v4);
            var (vR, sR) = genR.Generate(pcg);
            return (vR, new Size(s1.Append(sR), s2.Append(sR), s3.Append(sR), s4.Append(sR)));
        });
        public static Gen<R> SelectMany<T1, T2, T3, T4, T5, R>(this Gen<T1> gen1, Gen<T2> gen2, Gen<T3> gen3, Gen<T4> gen4,
            Gen<T5> gen5, Func<T1, T2, T3, T4, T5, Gen<R>> selector) => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var (v2, s2) = gen2.Generate(pcg);
            var (v3, s3) = gen3.Generate(pcg);
            var (v4, s4) = gen4.Generate(pcg);
            var (v5, s5) = gen5.Generate(pcg);
            var genR = selector(v1, v2, v3, v4, v5);
            var (vR, sR) = genR.Generate(pcg);
            return (vR, new Size(s1.Append(sR), s2.Append(sR), s3.Append(sR), s4.Append(sR), s5.Append(sR)));
        });
        public static Gen<R> SelectMany<T1, T2, R>(this Gen<T1> gen1, Func<T1, Gen<T2>> genSelector, Func<T1, T2, R> resultSelector)
            => Create(pcg =>
        {
            var (v1, s1) = gen1.Generate(pcg);
            var gen2 = genSelector(v1);
            var (v2, s2) = gen2.Generate(pcg);
            return (resultSelector(v1, v2), s1.Append(s2));
        });
        public static Gen<T> Where<T>(this Gen<T> gen, Func<T, bool> predicate) => Create(pcg =>
        {
            var t = gen.Generate(pcg);
            while (!predicate(t.Item1)) t = gen.Generate(pcg);
            return t;
        });
        public static Gen<T> Const<T>(T value) => Create(_ => (value, Size.Zero));
        public static Gen<T> Const<T>(Func<T> value) => Create(_ => (value(), Size.Zero));
        public static Gen<T> OneOf<T>(params T[] ts) => Int[0, ts.Length - 1].Select(i => ts[i]);
        public static Gen<T> OneOf<T>(params Gen<T>[] gens) => Int[0, gens.Length - 1].SelectMany(i => gens[i]);
        public static Gen<T> Enum<T>() where T : Enum
        {
            var a = System.Enum.GetValues(typeof(T));
            var ts = new T[a.Length];
            for (int i = 0; i < ts.Length; i++)
                ts[i] = (T)a.GetValue(i);
            return OneOf(ts);
        }
        public static Gen<T> Frequency<T>(params (int, T)[] ts)
        {
            var tsAgg = new (int, T)[ts.Length];
            int total = 0;
            for (int i = 0; i < ts.Length; i++)
            {
                var (f, g) = ts[i];
                total += f;
                tsAgg[i] = (total, g);
            }
            return Int[1, total].Select(c =>
            {
                for (int i = 0; i < tsAgg.Length - 1; i++)
                    if (c <= tsAgg[i].Item1)
                        return tsAgg[i].Item2;
                return tsAgg[tsAgg.Length - 1].Item2;
            });
        }
        public static Gen<T> Frequency<T>(params (int, Gen<T>)[] gens)
        {
            var gensAgg = new (int, Gen<T>)[gens.Length];
            int total = 0;
            for (int i = 0; i < gens.Length; i++)
            {
                var (f, g) = gens[i];
                total += f;
                gensAgg[i] = (total, g);
            }
            return Int[1, total].SelectMany(c =>
            {
                for (int i = 0; i < gensAgg.Length - 1; i++)
                    if (c <= gensAgg[i].Item1)
                        return gensAgg[i].Item2;
                return gensAgg[gensAgg.Length - 1].Item2;
            });
        }
        public static GenDictionary<K, V> Dictionary<K, V>(this Gen<K> genK, Gen<V> genV) => new(genK, genV);
        public static GenSortedDictionary<K, V> SortedDictionary<K, V>(this Gen<K> genK, Gen<V> genV) => new(genK, genV);
        public static IEnumerable<T> ToEnumerable<T>(this Gen<T> gen)
        {
            var pcg = PCG.ThreadPCG;
            while (true) yield return gen.Generate(pcg).Item1;
        }

        static void Shuffle<T>(IList<T> a, PCG pcg, int lower)
        {
            for (int i = a.Count - 1; i > lower; i--)
            {
                int j = (int)pcg.Next((uint)(i + 1));
                if (i != j)
                {
                    var temp = a[j];
                    a[j] = a[i];
                    a[i] = temp;
                }
            }
        }

        public static Gen<T[]> Shuffle<T>(T[] a) => Create(pcg =>
        {
            Shuffle(a, pcg, 0);
            return (a, Size.Zero);
        });

        public static Gen<T[]> Shuffle<T>(this Gen<T[]> gen) => Create(pcg =>
        {
            var (a, s) = gen.Generate(pcg);
            Shuffle(a, pcg, 0);
            return (a, s);
        });

        public static Gen<T[]> Shuffle<T>(T[] a, int length) => Create(pcg =>
        {
            int lower = Math.Max(a.Length - length, 0);
            Shuffle(a, pcg, lower);
            if (lower == 0) return (a, Size.Zero);
            var r = new T[length];
            for (int i = 0; i < r.Length; i++)
                r[i] = a[i + lower];
            return (r, Size.Zero);
        });

        public static Gen<T[]> Shuffle<T>(T[] a, int start, int finish) =>
            Int[start, finish].SelectMany(i => Shuffle(a, i));

        public static Gen<T[]> Shuffle<T>(this Gen<T[]> gen, int length) =>
            SelectMany(gen, a => Shuffle(a, length));

        public static Gen<T[]> Shuffle<T>(this Gen<T[]> gen, int start, int finish) =>
            SelectMany(gen, Int[start, finish], (a, l) => Shuffle(a, l));

        public static Gen<List<T>> Shuffle<T>(List<T> a) => Create(pcg =>
        {
            Shuffle(a, pcg, 0);
            return (a, Size.Zero);
        });

        public static Gen<List<T>> Shuffle<T>(this Gen<List<T>> gen) => Create(pcg =>
        {
            var (a, s) = gen.Generate(pcg);
            Shuffle(a, pcg, 0);
            return (a, s);
        });

        public static Gen<List<T>> Shuffle<T>(List<T> a, int length) => Create(pcg =>
        {
            int lower = Math.Max(a.Count - length, 0);
            Shuffle(a, pcg, lower);
            if (lower == 0) return (a, Size.Zero);
            var r = new List<T>(length);
            for (int i = 0; i < length; i++)
                r.Add(a[i + lower]);
            return (r, Size.Zero);
        });

        public static Gen<List<T>> Shuffle<T>(List<T> a, int start, int finish) =>
            SelectMany(Int[start, finish], l => Shuffle(a, l));

        public static Gen<List<T>> Shuffle<T>(this Gen<List<T>> gen, int length) =>
            SelectMany(gen, a => Shuffle(a, length));

        public static Gen<List<T>> Shuffle<T>(this Gen<List<T>> gen, int start, int finish) =>
            SelectMany(gen, Int[start, finish], (a, l) => Shuffle(a, l));

        public static Gen<(string, Action<T>)> Operation<T>(string name, Action<T> action)
        {
            return Create(pcg => ((name, action), Size.Zero));
        }

        public static readonly GenBool Bool = new();
        public static readonly GenSByte SByte = new();
        public static readonly GenByte Byte = new();
        public static readonly GenShort Short = new();
        public static readonly GenUShort UShort = new();
        public static readonly GenInt Int = new();
        public static readonly GenUInt UInt = new();
        public static readonly GenLong Long = new();
        public static readonly GenULong ULong = new();
        public static readonly GenFloat Float = new();
        public static readonly GenFloat Single = Float;
        public static readonly GenDouble Double = new();
        public static readonly GenDecimal Decimal = new();
        public static readonly GenDate Date = new();
        public static readonly GenDateTime DateTime = new();
        public static readonly GenTimeSpan TimeSpan = new();
        public static readonly GenDateTimeOffset DateTimeOffset = new();
        public static readonly GenGuid Guid = new();
        public static readonly GenChar Char = new();
        public static readonly GenString String = new();
    }

    public class GenBool : Gen<bool>
    {
        public override (bool, Size) Generate(PCG pcg)
        {
            uint i = pcg.Next();
            return ((i & 1u) == 1u, new Size(i));
        }
    }

    public class GenSByte : Gen<sbyte>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Zigzag(sbyte i) => (ulong)((i << 1) ^ (i >> 7));
        public override (sbyte, Size) Generate(PCG pcg)
        {
            sbyte i = (sbyte)(pcg.Next() & 255u);
            return (i, new Size(Zigzag(i)));
        }
        public Gen<sbyte> this[sbyte start, sbyte finish]
        {
            get
            {
                uint l = (uint)(finish - start) + 1u;
                return Gen.Create(pcg =>
                {
                    sbyte i = (sbyte)(start + pcg.Next(l));
                    return (i, new Size(Zigzag(i)));
                });
            }
        }
    }

    public class GenByte : Gen<byte>
    {
        public override (byte, Size) Generate(PCG pcg)
        {
            byte i = (byte)(pcg.Next() & 255u);
            return (i, new Size(i));
        }
        public Gen<byte> this[byte start, byte finish]
        {
            get
            {
                uint s = start;
                uint l = finish - s + 1u;
                return Gen.Create(pcg =>
                {
                    byte i = (byte)(s + pcg.Next(l));
                    return (i, new Size(i));
                });
            }
        }
    }

    public class GenShort : Gen<short>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Zigzag(short i) => (ulong)((i << 1) ^ (i >> 15));
        public override (short, Size) Generate(PCG pcg)
        {
            short i = (short)(pcg.Next() & 65535u);
            return (i, new Size(Zigzag(i)));
        }
        public Gen<short> this[short start, short finish]
        {
            get
            {
                uint l = (uint)(finish - start) + 1u;
                return Gen.Create(pcg =>
                {
                    short i = (short)(start + pcg.Next(l));
                    return (i, new Size(Zigzag(i)));
                });
            }
        }
    }

    public class GenUShort : Gen<ushort>
    {
        public override (ushort, Size) Generate(PCG pcg)
        {
            ushort i = (ushort)(pcg.Next() & 65535u);
            return (i, new Size(i));
        }
        public Gen<ushort> this[ushort start, ushort finish]
        {
            get
            {
                uint l = (uint)(finish - start) + 1u;
                return Gen.Create(pcg =>
                {
                    ushort i = (ushort)(start + pcg.Next(l));
                    return (i, new Size(i));
                });
            }
        }
    }

    public class GenInt : Gen<int>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Zigzag(int i) => (uint)((i << 1) ^ (i >> 31));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Unzigzag(uint i) => ((int)(i >> 1)) ^ -((int)(i & 1U));
        public override (int, Size) Generate(PCG pcg)
        {
            int i = (int)pcg.Next();
            return (i, new Size(Zigzag(i)));
        }
        public Gen<int> this[int start, int finish]
        {
            get
            {
                uint l = (uint)(finish - start) + 1u;
                return Gen.Create(pcg =>
                {
                    int i = (int)(start + pcg.Next(l));
                    return (i, new Size(Zigzag(i)));
                });
            }
        }
        public class IntSkew
        {
            public Gen<int> this[int start, int finish, double a] =>
                a >= 0.0 ? Gen.Double.Unit.Select(u => start + (int)(Math.Pow(u, a + 1.0) * (1.0 + finish - start)))
                : Gen.Double.Unit.Select(u => finish - (int)(Math.Pow(u, 1.0 - a) * (1.0 + finish - start)));
        }
        /// <summary>Skew the distribution towards either end.
        /// For a&gt;0 (positive skewness) the median decreases to 0.5*Math.Pow(0.5,a), and the mean decreases to 1.0/(1.0+a) of the range.
        /// For a&lt;0 (negative skewness) the median increases to 1.0-0.5*Math.Pow(0.5,-a), and the mean increases 1.0-1.0/(1.0-a) of the range.</summary>
        public IntSkew Skew = new();
    }

    public class GenUInt : Gen<uint>
    {
        public override (uint, Size) Generate(PCG pcg)
        {
            uint i = pcg.Next();
            return (i, new Size(i));
        }
        public Gen<uint> this[uint start, uint finish]
        {
            get
            {
                uint l = finish - start + 1u;
                return Gen.Create(pcg =>
                {
                    uint i = start + pcg.Next(l);
                    return (i, new Size(i));
                });
            }
        }
        public class UIntSkew
        {
            public Gen<uint> this[double a] => Gen.Double.Unit.Select(u =>
            {
                var ua = Math.Pow(u, a + 1.0);
                return (uint)(ua * uint.MaxValue + ua);
            });
            public Gen<uint> this[uint start, uint finish, double a] =>
                a >= 0.0 ? Gen.Double.Unit.Select(u => (uint)(start + Math.Pow(u, a + 1.0) * (1.0 + finish - start)))
                : Gen.Double.Unit.Select(u => (uint)(finish - Math.Pow(u, 1.0 - a) * (1.0 + finish - start)));
        }
        /// <summary>Skew the distribution towards either end.
        /// For a&gt;0 (positive skewness) the median decreases to 0.5*Math.Pow(0.5,a), and the mean decreases to 1.0/(1.0+a) of the range.
        /// For a&lt;0 (negative skewness) the median increases to 1.0-0.5*Math.Pow(0.5,-a), and the mean increases 1.0-1.0/(1.0-a) of the range.</summary>
        public UIntSkew Skew = new();
    }

    public class GenLong : Gen<long>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ulong Zigzag(long i) => (ulong)((i << 1) ^ (i >> 63));

        public override (long, Size) Generate(PCG pcg)
        {
            long i = (long)pcg.Next64();
            return (i, new Size(Zigzag(i)));
        }
        public Gen<long> this[long start, long finish]
        {
            get
            {

                ulong l = (ulong)(finish - start) + 1ul;
                return Gen.Create(pcg =>
                {
                    long i = start + (long)pcg.Next64(l);
                    return (i, new Size(Zigzag(i)));
                });
            }
        }
    }

    public class GenULong : Gen<ulong>
    {
        public override (ulong, Size) Generate(PCG pcg)
        {
            ulong i = pcg.Next64();
            return (i, new Size(i));
        }
        public Gen<ulong> this[ulong start, ulong finish]
        {
            get
            {
                ulong l = finish - start + 1ul;
                return Gen.Create(pcg =>
                {
                    ulong i = start + pcg.Next64(l);
                    return (i, new Size(i));
                });
            }
        }
    }

    public class GenFloat : Gen<float>
    {
        [StructLayout(LayoutKind.Explicit)]
        struct FloatConverter
        {
            [FieldOffset(0)] public uint I;
            [FieldOffset(0)] public float F;
        }
        public override (float, Size) Generate(PCG pcg)
        {
            uint i = pcg.Next();
            return (new FloatConverter { I = i }.F, new Size(i));
        }
        public Gen<float> this[float start, float finish]
        {
            get
            {
                finish -= start;
                start -= finish;
                return Gen.Create(pcg =>
                {
                    uint i = pcg.Next() >> 9;
                    return (new FloatConverter { I = i | 0x3F800000 }.F * finish + start
                            , new Size(i));
                });
            }
        }
        /// <summary>In the range 0.0 &lt;= x &lt;= max including special values.</summary>
        public Gen<float> NonNegative = Gen.Create(pcg =>
        {
            uint i = pcg.Next();
            return (Math.Abs(new FloatConverter { I = i }.F), new Size(i));
        });
        /// <summary>In the range 0.0f &lt;= x &lt; 1.0f.</summary>
        public Gen<float> Unit = Gen.Create(pcg =>
        {
            uint i = pcg.Next() >> 9;
            return (new FloatConverter { I = i | 0x3F800000 }.F - 1f, new Size(i));
        });
        /// <summary>In the range 1.0f &lt;= x &lt; 2.0f.</summary>
        public Gen<float> OneTwo = Gen.Create(pcg =>
        {
            uint i = pcg.Next() >> 9;
            return (new FloatConverter { I = i | 0x3F800000 }.F, new Size(i));
        });
        /// <summary>Without special values nan and inf.</summary>
        public Gen<float> Normal = Gen.Create(pcg =>
        {
            uint i = pcg.Next();
            return ((i & 0x7F800000U) == 0x7F800000U ? (8f - (i & 0xFU))
                    : new FloatConverter { I = i }.F
                , new Size(i));
        });
        /// <summary>In the range 0.0 &lt;= x &lt;= max without special values.</summary>
        public Gen<float> NormalNonNegative = Gen.Create(pcg =>
        {
            uint i = pcg.Next();
            return (Math.Abs((i & 0x7F800000U) == 0x7F800000U ? (8f - (i & 0xFU))
                    : new FloatConverter { I = i }.F)
                , new Size(i));
        });
        static float MakeSpecial(uint i) => (i & 0xFU) switch
        {
            0x0U => float.NaN,
            0x1U => float.PositiveInfinity,
            0x2U => float.NegativeInfinity,
            0x3U => float.MaxValue,
            0x4U => float.MinValue,
            0x5U => float.Epsilon,
            0x6U => -float.Epsilon,
            0x7U => 1f,
            0x8U => -1f,
            0x9U => 2f,
            0xAU => -2f,
            0xBU => 3f,
            0xCU => -3f,
            0xDU => 4f,
            0xEU => -4f,
            _ => 0f,
        };

        /// <summary>With more special values like nan, inf, max, epsilon, -2, -1, 0, 1, 2.</summary>
        public Gen<float> Special = Gen.Create(pcg =>
        {
            uint i = pcg.Next();
            return ((i & 0xF0U) == 0xD0U ? MakeSpecial(i)
                    : new FloatConverter { I = i }.F
                , new Size(i));
        });
    }

    public class GenDouble : Gen<double>
    {
        public override (double, Size) Generate(PCG pcg)
        {
            ulong i = pcg.Next64();
            return (BitConverter.Int64BitsToDouble((long)i), new Size(i));
        }
        public Gen<double> this[double start, double finish]
        {
            get
            {
                finish -= start;
                start -= finish;
                return Gen.Create(pcg =>
                {
                    ulong i = pcg.Next64() >> 12;
                    return (BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000) * finish + start
                            , new Size(i));
                });
            }
        }
        /// <summary>In the range 0.0 &lt;= x &lt;= max.</summary>
        public Gen<double> NonNegative = Gen.Create(pcg =>
        {
            ulong i = pcg.Next64();
            return (Math.Abs(BitConverter.Int64BitsToDouble((long)i)), new Size(i));
        });
        /// <summary>In the range 0.0 &lt;= x &lt; 1.0.</summary>
        public Gen<double> Unit = Gen.Create(pcg =>
        {
            ulong i = pcg.Next64() >> 12;
            return (BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000) - 1.0
                    , new Size(i));
        });
        /// <summary>In the range 1.0 &lt;= x &lt; 2.0.</summary>
        public Gen<double> OneTwo = Gen.Create(pcg =>
        {
            ulong i = pcg.Next64() >> 12;
            return (BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000)
                    , new Size(i));
        });
        /// <summary>Without special values nan and inf.</summary>
        public Gen<double> Normal = Gen.Create(pcg =>
        {
            ulong i = pcg.Next64();
            return ((i & 0x7FF0000000000000U) == 0x7FF0000000000000U ? (8.0 - (i & 0xFUL))
                  : BitConverter.Int64BitsToDouble((long)i)
                , new Size(i));
        });
        /// <summary>In the range 0.0 &lt;= x &lt;= max without special values nan and inf.</summary>
        public Gen<double> NormalNonNegative = Gen.Create(pcg =>
        {
            ulong i = pcg.Next64();
            return (Math.Abs((i & 0x7FF0000000000000U) == 0x7FF0000000000000U ? (8.0 - (i & 0xFUL))
                  : BitConverter.Int64BitsToDouble((long)i))
                , new Size(i));
        });
        static double MakeSpecial(ulong i) => (i & 0xFUL) switch
        {
            0x0UL => double.NaN,
            0x1UL => double.PositiveInfinity,
            0x2UL => double.NegativeInfinity,
            0x3UL => double.MaxValue,
            0x4UL => double.MinValue,
            0x5UL => double.Epsilon,
            0x6UL => -double.Epsilon,
            0x7UL => 1.0,
            0x8UL => -1.0,
            0x9UL => 2.0,
            0xAUL => -2.0,
            0xBUL => 3.0,
            0xCUL => -3.0,
            0xDUL => 4.0,
            0xEUL => -4.0,
            _ => 0.0,
        };
        /// <summary>With more special values like nan, inf, max, epsilon, -2, -1, 0, 1, 2.</summary>
        public Gen<double> Special = Gen.Create(pcg =>
        {
            ulong i = pcg.Next64();
            return ((i & 0xF0UL) == 0xD0UL ? MakeSpecial(i)
                    : BitConverter.Int64BitsToDouble((long)i)
                , new Size(i));
        });
        public class DoubleSkew
        {
            public Gen<double> this[double start, double finish, double a] =>
                a >= 0.0 ? Gen.Double.Unit.Select(u => start + Math.Pow(u, a + 1.0) * (finish - start))
                : Gen.Double.Unit.Select(u => finish - Math.Pow(u, 1.0 - a) * (finish - start));
        }
        /// <summary>Skew the distribution towards either end.
        /// For a&gt;0 (positive skewness) the median decreases to 0.5*Math.Pow(0.5,a), and the mean decreases to 1.0/(1.0+a) of the range.
        /// For a&lt;0 (negative skewness) the median increases to 1.0-0.5*Math.Pow(0.5,-a), and the mean increases 1.0-1.0/(1.0-a) of the range.</summary>
        public DoubleSkew Skew = new();
    }

    public class GenDecimal : Gen<decimal>
    {
        public override (decimal, Size) Generate(PCG pcg)
        {
            var scaleAndSign = (int)pcg.Next(58);
            var hi = (int)pcg.Next();
            var d = new decimal((int)pcg.Next(), (int)pcg.Next(), hi, (scaleAndSign & 1) == 1, (byte)(scaleAndSign >> 1));
            return (d, new Size((ulong)scaleAndSign << 32 + hi));
        }
        public Gen<decimal> this[decimal start, decimal finish]
        {
            get
            {
                finish -= start;
                start -= finish;
                return Gen.Create(pcg =>
                {
                    var i = pcg.Next64() >> 12;
                    return ((decimal)BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000) * finish + start
                            , new Size(i));
                });
            }
        }
        public Gen<decimal> NonNegative = Gen.Create(pcg =>
        {
            var scale = (byte)pcg.Next(29);
            var hi = (int)pcg.Next();
            var d = new decimal((int)pcg.Next(), (int)pcg.Next(), hi, false, scale);
            return (d, new Size((ulong)scale << 32 + hi));
        });
        public Gen<decimal> Unit = Gen.Create(pcg =>
        {
            ulong i = pcg.Next64() >> 12;
            return ((decimal)BitConverter.Int64BitsToDouble((long)i | 0x3FF0000000000000) - 1M
                    , new Size(i));
        });
    }

    public class GenDateTime : Gen<DateTime>
    {
        readonly ulong max = (ulong)DateTime.MaxValue.Ticks;
        public override (DateTime, Size) Generate(PCG pcg)
        {
            ulong i = pcg.Next64(max);
            return (new DateTime((long)i), new Size(i));
        }
        public Gen<DateTime> this[DateTime start, DateTime finish]
        {
            get
            {
                ulong l = (ulong)(finish.Ticks - start.Ticks) + 1ul;
                return Gen.Create(pcg =>
                {
                    ulong i = (ulong)start.Ticks + pcg.Next64(l);
                    return (new DateTime((long)i), new Size(i));
                });
            }
        }
    }

    public class GenDate : Gen<DateTime>
    {
        readonly uint max = (uint)(DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay);
        public override (DateTime, Size) Generate(PCG pcg)
        {
            uint i = pcg.Next(max);
            return (new DateTime(i * TimeSpan.TicksPerDay), new Size(i));
        }
        public Gen<DateTime> this[DateTime start, DateTime finish]
        {
            get
            {
                uint s = (uint)(start.Ticks / TimeSpan.TicksPerDay);
                uint l = (uint)((finish.Ticks - start.Ticks) / TimeSpan.TicksPerDay) + 1u;
                return Gen.Create(pcg =>
                {
                    uint i = s + pcg.Next(l);
                    return (new DateTime(i * TimeSpan.TicksPerDay), new Size(i));
                });
            }
        }
    }

    public class GenTimeSpan : Gen<TimeSpan>
    {
        public override (TimeSpan, Size) Generate(PCG pcg)
        {
            ulong i = pcg.Next64();
            return (new TimeSpan((long)i), new Size(i));
        }
        public Gen<TimeSpan> this[TimeSpan start, TimeSpan finish]
        {
            get
            {
                ulong l = (ulong)(finish.Ticks - start.Ticks) + 1ul;
                return Gen.Create(pcg =>
                {
                    ulong i = (ulong)start.Ticks + pcg.Next64(l);
                    return (new TimeSpan((long)i), new Size(i));
                });
            }
        }
    }

    public class GenDateTimeOffset : Gen<DateTimeOffset>
    {
        readonly Gen<DateTime> genDateTime = Gen.DateTime[new DateTime(1800, 1, 1), new DateTime(2200, 1, 1)];
        readonly Gen<int> genOffset = Gen.Int[-14 * 60, 14 * 60];
        public override (DateTimeOffset, Size) Generate(PCG pcg)
        {
            var (os, s1) = genOffset.Generate(pcg);
            var (dt, s2) = genDateTime.Generate(pcg);
            return (new DateTimeOffset(dt, TimeSpan.FromMinutes(os)), new Size(s1.I, new[] { s2 }));
        }
    }

    public class GenGuid : Gen<Guid>
    {
        [StructLayout(LayoutKind.Explicit)]
        struct GuidConverter
        {
            [FieldOffset(0)] public Guid G;
            [FieldOffset(0)] public uint I0;
            [FieldOffset(4)] public uint I1;
            [FieldOffset(8)] public uint I2;
            [FieldOffset(12)] public uint I3;
        }
        public override (Guid, Size) Generate(PCG pcg)
        {
            var c = new GuidConverter { I0 = pcg.Next(), I1 = pcg.Next(), I2 = pcg.Next(), I3 = pcg.Next() };
            return (c.G,
                new Size(new Size(c.I0), new Size(c.I1), new Size(c.I2), new Size(c.I3)));
        }
    }

    public class GenChar : Gen<char>
    {
        public override (char, Size) Generate(PCG pcg)
        {
            var i = pcg.Next() & 127u;
            return ((char)i, new Size(i));
        }
        public Gen<char> this[char start, char finish]
        {
            get
            {
                uint s = start;
                uint l = finish + 1u - s;
                return Gen.Create(pcg =>
                {
                    var i = pcg.Next(l);
                    return ((char)(s + i), new Size(i));
                });
            }
        }
        public Gen<char> this[string chars]
        {
            get
            {
                return Gen.Create(pcg =>
                {
                    var i = pcg.Next((uint)chars.Length);
                    return (chars[(int)i], new Size(i));
                });
            }
        }
    }

    public class GenString : Gen<string>
    {
        static readonly Gen<string> d = Gen.Char.Array.Select(i => new string(i));
        public override (string, Size) Generate(PCG pcg) => d.Generate(pcg);
        public Gen<string> this[int start, int finish] =>
            Gen.Char.Array[start, finish].Select(i => new string(i));
        public Gen<string> this[Gen<char> gen, int start, int finish] =>
            gen.Array[start, finish].Select(i => new string(i));
        public Gen<string> this[Gen<char> gen] =>
            gen.Array.Select(i => new string(i));
        public Gen<string> this[string chars] =>
            Gen.Char[chars].Array.Select(i => new string(i));
    }

    public class GenArray<T> : Gen<T[]>
    {
        readonly Gen<T> gen;
        public GenArray(Gen<T> gen) => this.gen = gen;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        (T[], Size) Generate(PCG pcg, int length, ulong size)
        {
            var vs = new T[length];
            var ss = new Size[length];
            for (int i = 0; i < vs.Length; i++)
            {
                var (v, s) = gen.Generate(pcg);
                vs[i] = v;
                ss[i] = s;
            }
            return (vs, new Size(size, ss));
        }
        public override (T[], Size) Generate(PCG pcg)
        {
            var l = pcg.Next() & 127U;
            return Generate(pcg, (int)l, ((ulong)l) << 32);
        }
        public Gen<T[]> this[Gen<int> length] => Gen.Create(pcg =>
        {
            var (l, sl) = length.Generate(pcg);
            return Generate(pcg, l, sl.I << 32);
        });
        public Gen<T[]> this[int length] => Gen.Create(pcg =>
        {
            return Generate(pcg, length, 0UL);
        });
        public Gen<T[]> this[int start, int finish] => this[Gen.Int[start, finish]];
    }

    public class GenArrayUnique<T> : Gen<T[]>
    {
        readonly Gen<T> gen;
        public GenArrayUnique(Gen<T> gen) => this.gen = gen;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        (T[], Size) Generate(PCG pcg, int length, ulong size)
        {
            var vs = new T[length];
            var ss = new Size[length];
            var hs = new HashSet<T>();
            int i = 0;
            while(i < length)
            {

                var (v, s) = gen.Generate(pcg);
                var bad = 0;
                if (hs.Add(v))
                {
                    vs[i] = v;
                    ss[i++] = s;
                }
                else if (++bad == 1000) throw new CsCheckException("Failing to add to ArrayUnique");
            }
            return (vs, new Size(size, ss));
        }
        public override (T[], Size) Generate(PCG pcg)
        {
            var l = pcg.Next() & 127U;
            return Generate(pcg, (int)l, ((ulong)l) << 32);
        }
        public Gen<T[]> this[Gen<int> length] => Gen.Create(pcg =>
        {
            var (l, sl) = length.Generate(pcg);
            return Generate(pcg, l, sl.I << 32);
        });
        public Gen<T[]> this[int length] => Gen.Create(pcg =>
        {
            return Generate(pcg, length, 0UL);
        });
        public Gen<T[]> this[int start, int finish] => this[Gen.Int[start, finish]];
    }

    public class GenEnumerable<T> : Gen<IEnumerable<T>>
    {
        readonly Gen<T> gen;
        public GenEnumerable(Gen<T> gen) => this.gen = gen;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        (IEnumerable<T>, Size) Generate(PCG pcg, int length, ulong size)
        {
            var vs = new T[length];
            var ss = new Size[length];
            for (int i = 0; i < vs.Length; i++)
            {
                var (v, s) = gen.Generate(pcg);
                vs[i] = v;
                ss[i] = s;
            }
            return (vs, new Size(size, ss));
        }
        public override (IEnumerable<T>, Size) Generate(PCG pcg)
        {
            var l = pcg.Next() & 127U;
            return Generate(pcg, (int)l, ((ulong)l) << 32);
        }
        public Gen<IEnumerable<T>> this[Gen<int> length] => Gen.Create(pcg =>
        {
            var (l, sl) = length.Generate(pcg);
            return Generate(pcg, l, sl.I << 32);
        });
        public Gen<IEnumerable<T>> this[int length] => Gen.Create(pcg =>
        {
            return Generate(pcg, length, 0UL);
        });
        public Gen<IEnumerable<T>> this[int start, int finish] => this[Gen.Int[start, finish]];
    }

    public class GenArray2D<T> : Gen<T[,]>
    {
        readonly Gen<T> gen;
        public GenArray2D(Gen<T> gen) => this.gen = gen;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        (T[,], Size) Generate(PCG pcg, int length0, int length1, ulong size)
        {
            var vs = new T[length0, length1];
            for (int i = 0; i < length0; i++)
                for (int j = 0; j < length1; j++)
                {
                    var v = gen.Generate(pcg).Item1;
                    vs[i, j] = v;
                }
            return (vs, new Size(size));
        }
        public override (T[,], Size) Generate(PCG pcg)
        {
            var l0 = pcg.Next() & 127U;
            var l1 = pcg.Next() & 127U;
            return Generate(pcg, (int)l0, (int)l1, ((ulong)(l0 * l1)) << 32);
        }
        public Gen<T[,]> this[int length0, int length1] => Gen.Create(pcg =>
        {
            return Generate(pcg, length0, length1, 0UL);
        });
        public Gen<T[,]> this[Gen<int> length0, Gen<int> length1] => Gen.Create(pcg =>
        {
            var (l0, s0) = length0.Generate(pcg);
            var (l1, s1) = length1.Generate(pcg);
            return Generate(pcg, l0, l1, (s0.I + s1.I) << 32);
        });
    }

    public class GenList<T> : Gen<List<T>>
    {
        readonly Gen<T> gen;
        public GenList(Gen<T> gen) => this.gen = gen;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        (List<T>, Size) Generate(PCG pcg, int length, ulong size)
        {
            var vs = new List<T>(length);
            var ss = new Size[length];
            for (int i = 0; i < ss.Length; i++)
            {
                var (v, s) = gen.Generate(pcg);
                vs.Add(v);
                ss[i] = s;
            }
            return (vs, new Size(size, ss));
        }
        public override (List<T>, Size) Generate(PCG pcg)
        {
            uint l = pcg.Next() & 127U;
            return Generate(pcg, (int)l, ((ulong)l) << 32);
        }
        public Gen<List<T>> this[Gen<int> length] => Gen.Create(pcg =>
        {
            var (l, sl) = length.Generate(pcg);
            return Generate(pcg, l, sl.I << 32);
        });
        public Gen<List<T>> this[int length] => Gen.Create(pcg =>
        {
            return Generate(pcg, length, 0UL);
        });
        public Gen<List<T>> this[int start, int finish] => this[Gen.Int[start, finish]];
    }

    public class GenHashSet<T> : Gen<HashSet<T>>
    {
        readonly Gen<T> gen;
        public GenHashSet(Gen<T> gen) => this.gen = gen;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        (HashSet<T>, Size) Generate(PCG pcg, int length, ulong size)
        {
            var vs = new HashSet<T>();
            var ss = new Size[length];
            var bad = 0;
            while (length > 0)
            {
                var (v, s) = gen.Generate(pcg);
                if (vs.Add(v))
                {
                    ss[--length] = s;
                    bad = 0;
                }
                else if (++bad == 1000) throw new CsCheckException("Failing to add to HashSet");
            }
            return (vs, new Size(size, ss));
        }
        public override (HashSet<T>, Size) Generate(PCG pcg)
        {
            var l = pcg.Next() & 127U;
            return Generate(pcg, (int)l, ((ulong)l) << 32);
        }
        public Gen<HashSet<T>> this[Gen<int> length] => Gen.Create(pcg =>
        {
            var (l, sl) = length.Generate(pcg);
            return Generate(pcg, l, sl.I << 32);
        });
        public Gen<HashSet<T>> this[int length] => Gen.Create(pcg =>
        {
            return Generate(pcg, length, 0UL);
        });
        public Gen<HashSet<T>> this[int start, int finish] => this[Gen.Int[start, finish]];
    }

    public class GenDictionary<K, V> : Gen<Dictionary<K, V>>
    {
        readonly Gen<K> genK;
        readonly Gen<V> genV;
        public GenDictionary(Gen<K> genK, Gen<V> genV)
        {
            this.genK = genK;
            this.genV = genV;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        (Dictionary<K, V>, Size) Generate(PCG pcg, int length, ulong size)
        {
            var vs = new Dictionary<K, V>(length);
            var ss = new Size[2 * length];
            var i = length;
            var bad = 0;
            while (i > 0)
            {
                var (k, sk) = genK.Generate(pcg);
                if (!vs.ContainsKey(k))
                {
                    var (v, sv) = genV.Generate(pcg);
                    vs.Add(k, v);
                    ss[--i] = sk;
                    ss[length + i] = sv;
                    bad = 0;
                }
                else if (++bad == 1000) throw new CsCheckException("Failing to add to Dictionary");
            }
            return (vs, new Size(size, ss));
        }
        public override (Dictionary<K, V>, Size) Generate(PCG pcg)
        {
            var l = pcg.Next() & 127U;
            return Generate(pcg, (int)l, ((ulong)l) << 32);
        }
        public Gen<Dictionary<K, V>> this[Gen<int> length] => Gen.Create(pcg =>
        {
            var (l, sl) = length.Generate(pcg);
            return Generate(pcg, l, sl.I << 32);
        });
        public Gen<Dictionary<K, V>> this[int length] => Gen.Create(pcg =>
        {
            return Generate(pcg, length, 0UL);
        });
        public Gen<Dictionary<K, V>> this[int start, int finish] => this[Gen.Int[start, finish]];
    }

    public class GenSortedDictionary<K, V> : Gen<SortedDictionary<K, V>>
    {
        readonly Gen<K> genK;
        readonly Gen<V> genV;
        public GenSortedDictionary(Gen<K> genK, Gen<V> genV)
        {
            this.genK = genK;
            this.genV = genV;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        (SortedDictionary<K, V>, Size) Generate(PCG pcg, int length, ulong size)
        {
            var vs = new SortedDictionary<K, V>();
            var ss = new Size[2 * length];
            var i = length;
            var bad = 0;
            while (i > 0)
            {
                var (k, sk) = genK.Generate(pcg);
                if (!vs.ContainsKey(k))
                {
                    var (v, sv) = genV.Generate(pcg);
                    vs.Add(k, v);
                    ss[--i] = sk;
                    ss[length + i] = sv;
                    bad = 0;
                }
                else if (++bad == 1000) throw new CsCheckException("Failing to add to SortedDictionary");
            }
            return (vs, new Size(size, ss));
        }
        public override (SortedDictionary<K, V>, Size) Generate(PCG pcg)
        {
            var l = pcg.Next() & 127U;
            return Generate(pcg, (int)l, ((ulong)l) << 32);
        }
        public Gen<SortedDictionary<K, V>> this[Gen<int> length] => Gen.Create(pcg =>
        {
            var (l, sl) = length.Generate(pcg);
            return Generate(pcg, l, sl.I << 32);
        });
        public Gen<SortedDictionary<K, V>> this[int length] => Gen.Create(pcg =>
        {
            return Generate(pcg, length, 0UL);
        });
        public Gen<SortedDictionary<K, V>> this[int start, int finish] => this[Gen.Int[start, finish]];
    }
}