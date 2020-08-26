﻿// Copyright 2020 Anthony Lloyd
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
using System.Diagnostics;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CsCheck
{
    public class CsCheckException : Exception
    {
        public CsCheckException(string message) : base(message) { }
        public CsCheckException(string message, Exception exception) : base(message, exception) { }
    }

    public static class Check
    {
        public static string Seed;
        public static int Size = 100;
        public static double Sigma;
        static Check()
        {
            var seed = Environment.GetEnvironmentVariable("CsCheck_Seed");
            if (!string.IsNullOrWhiteSpace(seed)) Seed = PCG.Parse(seed).ToString();
            var size = Environment.GetEnvironmentVariable("CsCheck_Size");
            if (!string.IsNullOrWhiteSpace(size)) Size = int.Parse(size);
            var sigma = Environment.GetEnvironmentVariable("CsCheck_Sigma");
            if (!string.IsNullOrWhiteSpace(sigma)) Sigma = double.Parse(sigma);
        }
        /// <summary>Sample the gen calling the assert each time across multiple threads. Shrink any exceptions if necessary.</summary>
        public static void Sample<T>(this Gen<T> gen, Action<T> assert, string seed = null, int size = -1, int threads = -1)
        {
            if (size == -1) size = Size;
            PCG minPCG = null;
            ulong minState = 0UL;
            Size minSize = null;
            Exception minException = null;

            if (seed != null || Seed != null)
            {
                var pcg = PCG.Parse(seed ?? Seed);
                ulong state = pcg.State;
                Size s = null;
                try
                {
                    var t = gen.Generate(pcg);
                    s = t.Item2;
                    assert(t.Item1);
                }
                catch (Exception e)
                {
                    minPCG = pcg;
                    minState = state;
                    minSize = s;
                    minException = e;
                }
                size--;
            }

            var lockObj = new object();
            int shrinks = 0, skipped = 0;
            Parallel.For(0, size, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                var pcg = PCG.ThreadPCG;
                ulong state = pcg.State;
                Size s = null;
                try
                {
                    var t = gen.Generate(pcg);
                    s = t.Item2;
                    if (minSize is null || s.IsLessThan(minSize))
                        assert(t.Item1);
                    else
                        skipped++;
                }
                catch (Exception e)
                {
                    lock (lockObj)
                    {
                        if (minSize is null || s.IsLessThan(minSize))
                        {
                            if (minSize is object) shrinks++;
                            minPCG = pcg;
                            minState = state;
                            minSize = s;
                            minException = e;
                        }
                    }
                }
            });

            if (minPCG != null) throw new CsCheckException(
                $"CsCheck_Seed = \"{minPCG.ToString(minState)}\" ({shrinks:#,0} shrinks, {skipped:#,0} skipped, {size:#,0} total)"
                    , minException);
        }
        /// <summary>Sample the gen calling the predicate each time across multiple threads. Shrink any exceptions if necessary.</summary>
        public static void Sample<T>(this Gen<T> gen, Func<T, bool> predicate, string seed = null, int size = -1, int threads = -1)
        {
            if (size == -1) size = Size;
            PCG minPCG = null;
            ulong minState = 0UL;
            Size minSize = null;
            Exception minException = null;

            if (seed != null || Seed != null)
            {
                var pcg = PCG.Parse(seed ?? Seed);
                ulong state = pcg.State;
                Size s = null;
                try
                {
                    var t = gen.Generate(pcg);
                    s = t.Item2;
                    if (!predicate(t.Item1))
                    {
                        minPCG = pcg;
                        minState = state;
                        minSize = s;
                    }
                }
                catch (Exception e)
                {
                    minPCG = pcg;
                    minState = state;
                    minSize = s;
                    minException = e;
                }
                size--;
            }

            var lockObj = new object();
            int shrinks = 0, skipped = 0;
            Parallel.For(0, size, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
            {
                var pcg = PCG.ThreadPCG;
                ulong state = pcg.State;
                Size s = null;
                try
                {
                    var t = gen.Generate(pcg);
                    s = t.Item2;
                    if (minSize is null || s.IsLessThan(minSize))
                    {
                        if (!predicate(t.Item1))
                        {
                            lock (lockObj)
                            {
                                if (minSize is null || s.IsLessThan(minSize))
                                {
                                    if (minSize is object) shrinks++;
                                    minPCG = pcg;
                                    minState = state;
                                    minSize = s;
                                }
                            }
                        }
                    }
                    else skipped++;
                }
                catch (Exception e)
                {
                    lock (lockObj)
                    {
                        if (minSize is null || s.IsLessThan(minSize))
                        {
                            if (minSize is object) shrinks++;
                            minPCG = pcg;
                            minState = state;
                            minSize = s;
                            minException = e;
                        }
                    }
                }
            });

            if (minPCG != null) throw new CsCheckException(
                $"CsCheck_Seed = \"{minPCG.ToString(minState)}\" ({shrinks:#,0} shrinks, {skipped:#,0} skipped, {size:#,0} total)"
                    , minException);
        }
        /// <summary>Sample the gen once calling the assert.</summary>
        public static void SampleOne<T>(this Gen<T> gen, Action<T> assert, string seed = null)
        {
            Sample(gen, assert, seed, 1, 1);
        }
        /// <summary>Sample the gen once calling the predicate.</summary>
        public static void SampleOne<T>(this Gen<T> gen, Func<T, bool> predicate, string seed = null)
        {
            Sample(gen, predicate, seed, 1, 1);
        }
        /// <summary>Sample the gen and assert pairs each time across multiple threads. Useful for multithreading tests.</summary>
        public static void Sample<T1, T2>(Gen<T1> gen1, Action<T1> assert1, Gen<T2> gen2, Action<T2> assert2,
            string seed = null, int size = -1, int threads = -1)
        {
            try
            {
                Sample(
                    Gen.Bool.SelectMany(b => b ? gen1.Select(t => (b, (object)t))
                                               : gen2.Select(t => (b, (object)t))),
                    t => { if (t.b) assert1((T1)t.Item2); else assert2((T2)t.Item2); },
                    seed, size, threads
                );
            }
            catch (CsCheckException e)
            {
                throw e.InnerException; // remove seed info as it's not reproducible.
            }
        }
        /// <summary>Sample the gen and assert pairs each time across multiple threads. Useful for multithreading tests.</summary>
        public static void Sample<T1, T2, T3>(Gen<T1> gen1, Action<T1> assert1, Gen<T2> gen2, Action<T2> assert2,
            Gen<T3> gen3, Action<T3> assert3, string seed = null, int size = -1, int threads = -1)
        {
            try
            {
                Sample(
                    Gen.Int[0, 2].SelectMany(i => i == 0 ? gen1.Select(t => (i, (object)t))
                                                : i == 1 ? gen2.Select(t => (i, (object)t))
                                                : gen3.Select(t => (i, (object)t))),
                    t =>
                    {
                        if (t.i == 0) assert1((T1)t.Item2);
                        else if (t.i == 1) assert2((T2)t.Item2);
                        else assert3((T3)t.Item2);
                    },
                    seed, size, threads
                );
            }
            catch (CsCheckException e)
            {
                throw e.InnerException; // remove seed info as it's not reproducible.
            }
        }
        /// <summary>Sample the gen and assert pairs each time across multiple threads. Useful for multithreading tests.</summary>
        public static void Sample<T1, T2, T3, T4>(Gen<T1> gen1, Action<T1> assert1, Gen<T2> gen2, Action<T2> assert2,
            Gen<T3> gen3, Action<T3> assert3, Gen<T4> gen4, Action<T4> assert4, string seed = null, int size = -1, int threads = -1)
        {
            try
            {
                Sample(
                    Gen.Int.SelectMany(i =>
                    {
                        i &= 3;
                        return i == 0 ? gen1.Select(t => (i, (object)t))
                             : i == 1 ? gen2.Select(t => (i, (object)t))
                             : i == 2 ? gen3.Select(t => (i, (object)t))
                             : gen4.Select(t => (i, (object)t));
                    }),
                    t =>
                    {
                        switch (t.i & 3)
                        {
                            case 0: assert1((T1)t.Item2); break;
                            case 1: assert2((T2)t.Item2); break;
                            case 2: assert3((T3)t.Item2); break;
                            default: assert4((T4)t.Item2); break;
                        }
                    },
                    seed, size, threads
                );
            }
            catch (CsCheckException e)
            {
                throw e.InnerException; // remove seed info as it's not reproducible.
            }
        }
        /// <summary>Assert actual is in line with expected using a chi-squared test to 6 sigma.</summary>
        public static void ChiSquared(int[] expected, int[] actual)
        {
            if (expected.Length != actual.Length) throw new CsCheckException("Expected and actual lengths need to be the same.");
            if (Array.Exists(expected, e => e <= 5)) throw new CsCheckException("Expected frequency for all buckets needs to be above 5.");
            double chi = 0.0;
            for (int i = 0; i < expected.Length; i++)
            {
                double e = expected[i];
                double d = actual[i] - e;
                chi += d * d / e;
            }
            // chi-squared distribution has Mean = k and Variance = 2 k where k is the number of degrees of freedom.
            int k = expected.Length - 1;
            double sigmaSquared = (chi - k) * (chi - k) / k / 2.0;
            if (sigmaSquared > 36.0) throw new CsCheckException(
                "Chi-squared standard deviation = " + Math.Sqrt(sigmaSquared).ToString("0.0"));
        }

        /// <summary>Assert the first Action is faster than the second to a given sigma (defaults to 6).</summary>
        public static FasterResult Faster(Action faster, Action slower,
            double sigma = -1.0, int threads = -1, int repeat = 1, int timeout = 60_000, bool raiseexception = true)
        {
            if (sigma == -1.0) sigma = Sigma == 0.0 ? 6.0 : Sigma;
            sigma *= sigma;
            if (threads == -1) threads = Environment.ProcessorCount;
            var r = new FasterResult { Median = new MedianEstimator() };
            var mre = new ManualResetEventSlim();
            Exception exception = null;
            while (threads-- > 0)
                Task.Run(() =>
                {
                    try
                    {
                        while (!mre.IsSet)
                        {
                            var tf = Stopwatch.GetTimestamp();
                            for (int i = 1; i < repeat; i++) faster();
                            faster();
                            tf = Stopwatch.GetTimestamp() - tf;
                            if (mre.IsSet) return;
                            var ts = Stopwatch.GetTimestamp();
                            for (int i = 1; i < repeat; i++) slower();
                            slower();
                            ts = Stopwatch.GetTimestamp() - ts;
                            if (mre.IsSet) return;
                            var e = (float)(ts - tf) / Math.Max(ts, tf);
                            lock (r)
                            {
                                r.Median.Add(e);
                                if (tf < ts) r.Faster++;
                                else if (tf > ts) r.Slower++;
                            }
                            if (r.SigmaSquared >= sigma) mre.Set();
                        }
                    }
                    catch (Exception e)
                    {
                        exception = e;
                        mre.Set();
                    }
                });
            bool completed = mre.Wait(timeout);
            if (raiseexception)
            {
                if (!completed) throw new CsCheckException("Timeout! " + r.ToString());
                if (exception != null || r.Slower > r.Faster) throw exception ?? new CsCheckException(r.ToString());
            }
            return r;
        }
        /// <summary>Assert the first Func gives the same result and is faster than the second to a given sigma (defaults to 6).</summary>
        public static FasterResult Faster<T>(Func<T> faster, Func<T> slower, Action<T, T> assertEqual = null,
            double sigma = -1.0, int threads = -1, int repeat = 1, int timeout = 60_000, bool raiseexception = true)
        {
            if (sigma == -1.0) sigma = Sigma == 0.0 ? 6.0 : Sigma;
            sigma *= sigma;
            if (threads == -1) threads = Environment.ProcessorCount;
            var r = new FasterResult { Median = new MedianEstimator() };
            var mre = new ManualResetEventSlim();
            Exception exception = null;
            while (threads-- > 0)
                Task.Run(() =>
                {
                    try
                    {
                        while (!mre.IsSet)
                        {
                            var tf = Stopwatch.GetTimestamp();
                            for (int i = 1; i < repeat; i++) faster();
                            var vf = faster();
                            tf = Stopwatch.GetTimestamp() - tf;
                            if (mre.IsSet) return;
                            var ts = Stopwatch.GetTimestamp();
                            for (int i = 1; i < repeat; i++) slower();
                            var vs = slower();
                            ts = Stopwatch.GetTimestamp() - ts;
                            if (mre.IsSet) return;
                            if (assertEqual == null)
                            {
                                if (!vf.Equals(vs))
                                {
                                    exception = new CsCheckException($"Return values differ: faster={vf} slower={vs}");
                                    mre.Set();
                                    return;
                                }
                            }
                            else
                            {
                                try
                                {
                                    assertEqual(vf, vs);
                                }
                                catch (Exception ex)
                                {
                                    exception = ex;
                                    mre.Set();
                                    return;
                                }
                            }
                            var e = (float)(ts - tf) / Math.Max(ts, tf);
                            lock (r)
                            {
                                r.Median.Add(e);
                                if (tf < ts) r.Faster++;
                                else if (tf > ts) r.Slower++;
                            }
                            if (r.SigmaSquared >= sigma) mre.Set();
                        }
                    }
                    catch (Exception e)
                    {
                        exception = e;
                        mre.Set();
                    }
                });
            bool completed = mre.Wait(timeout);
            if (raiseexception)
            {
                if (!completed) throw new CsCheckException("Timeout! " + r.ToString());
                if (exception != null || r.Slower > r.Faster) throw exception ?? new CsCheckException(r.ToString());
            }
            return r;
        }
        /// <summary>Assert the first Action is faster than the second to a given sigma (defaults to 6) across a sampel of input data.</summary>
        public static FasterResult Faster<T>(this Gen<T> gen, Action<T> faster, Action<T> slower,
            double sigma = -1.0, int threads = -1, int repeat = 1, int timeout = 60_000, string seed = null, bool raiseexception = true)
        {
            if (sigma == -1.0) sigma = Sigma == 0.0 ? 6.0 : Sigma;
            sigma *= sigma; // using sigma as sigma squared now
            if (seed == null) seed = Seed;
            if (threads == -1) threads = Environment.ProcessorCount;
            var r = new FasterResult { Median = new MedianEstimator() };
            var mre = new ManualResetEventSlim();
            Exception exception = null;
            while (threads-- > 0)
                Task.Run(() =>
                {
                    var pcg = seed == null ? PCG.ThreadPCG : PCG.Parse(seed);
                    ulong state = 0;
                    T t = default;
                    try
                    {
                        while (!mre.IsSet)
                        {
                            state = pcg.State;
                            t = gen.Generate(pcg).Item1;
                            var tf = Stopwatch.GetTimestamp();
                            for (int i = 1; i < repeat; i++) faster(t);
                            faster(t);
                            tf = Stopwatch.GetTimestamp() - tf;
                            if (mre.IsSet) return;
                            var ts = Stopwatch.GetTimestamp();
                            for (int i = 1; i < repeat; i++) slower(t);
                            slower(t);
                            ts = Stopwatch.GetTimestamp() - ts;
                            if (mre.IsSet) return;
                            var e = (float)(ts - tf) / Math.Max(ts, tf);
                            lock (r)
                            {
                                r.Median.Add(e);
                                if (tf < ts) r.Faster++;
                                else if (tf > ts) r.Slower++;
                            }
                            if (r.SigmaSquared >= sigma) mre.Set();
                        }
                    }
                    catch (Exception e)
                    {
                        var tstring = t.ToString();
                        if (tstring.Length > 100) tstring = tstring.Substring(0, 100);
                        exception = new CsCheckException($"CsCheck_Seed = \"{pcg.ToString(state)}\" T={tstring}", e);
                        mre.Set();
                    }
                });
            var completed = mre.Wait(timeout);
            if (raiseexception)
            {
                if (!completed) throw new CsCheckException("Timeout! " + r.ToString());
                if (exception != null || r.Slower > r.Faster) throw exception ?? new CsCheckException(r.ToString());
            }
            return r;
        }
        /// <summary>Assert the first Func gives the same result and is faster than the second to a given sigma (defaults to 6) across a sample of input data.</summary>
        public static FasterResult Faster<T1, T2>(this Gen<T1> gen, Func<T1, T2> faster, Func<T1, T2> slower, Action<T2, T2> assertEqual = null,
            double sigma = -1.0, int threads = -1, int repeat = 1, int timeout = 60_000, string seed = null, bool raiseexception = true)
        {
            if (sigma == -1.0) sigma = Sigma == 0.0 ? 6.0 : Sigma;
            sigma *= sigma; // using sigma as sigma squared now
            if (seed == null) seed = Seed;
            if (threads == -1) threads = Environment.ProcessorCount;
            var r = new FasterResult { Median = new MedianEstimator() };
            var mre = new ManualResetEventSlim();
            Exception exception = null;
            while (threads-- > 0)
                Task.Run(() =>
                {
                    var pcg = seed == null ? PCG.ThreadPCG : PCG.Parse(seed);
                    ulong state = 0;
                    T1 t = default;
                    try
                    {

                        while (!mre.IsSet)
                        {
                            state = pcg.State;
                            t = gen.Generate(pcg).Item1;
                            var tf = Stopwatch.GetTimestamp();
                            for (int i = 1; i < repeat; i++) faster(t);
                            var vf = faster(t);
                            tf = Stopwatch.GetTimestamp() - tf;
                            if (mre.IsSet) return;
                            var ts = Stopwatch.GetTimestamp();
                            for (int i = 1; i < repeat; i++) slower(t);
                            var vs = slower(t);
                            ts = Stopwatch.GetTimestamp() - ts;
                            if (mre.IsSet) return;
                            if (assertEqual == null)
                            {
                                if (!vf.Equals(vs))
                                {
                                    exception = new CsCheckException(
                                        $"Return values differ: CsCheck_Seed = \"{pcg.ToString(state)}\" faster={vf} slower={vs}");
                                    mre.Set();
                                    return;
                                }
                            }
                            else
                            {
                                try
                                {
                                    assertEqual(vf, vs);
                                }
                                catch (Exception ex)
                                {
                                    exception = new CsCheckException(
                                        $"Return values differ: CsCheck_Seed = \"" + pcg.ToString(state) + "\"", ex);
                                    mre.Set();
                                    return;
                                }
                            }
                            var e = (float)(ts - tf) / Math.Max(ts, tf);
                            lock (r)
                            {
                                r.Median.Add(e);
                                if (tf < ts) r.Faster++;
                                else if (tf > ts) r.Slower++;
                            }
                            if (r.SigmaSquared >= sigma) mre.Set();
                        }
                    }
                    catch (Exception e)
                    {
                        var tstring = t.ToString();
                        if (tstring.Length > 100) tstring = tstring.Substring(0, 100);
                        exception = new CsCheckException($"CsCheck_Seed = \"{pcg.ToString(state)}\" T={tstring}", e);
                        mre.Set();
                    }
                });
            var completed = mre.Wait(timeout);
            if (raiseexception)
            {
                if (!completed) throw new CsCheckException("Timeout! " + r.ToString());
                if (exception != null || r.Slower > r.Faster) throw exception ?? new CsCheckException(r.ToString());
            }
            return r;
        }
        /// <summary>Generate an example that satisfies the predicate.</summary>
        public static T Example<T>(this Gen<T> g, Func<T, bool> predicate, string seed = null, Action<string> output = null)
        {
            if (seed == null)
            {
                var mre = new ManualResetEventSlim();
                T ret = default;
                string message = null;
                var threads = Environment.ProcessorCount;
                while (threads-- > 0)
                    Task.Run(() =>
                    {
                        var pcg = PCG.ThreadPCG;
                        while (true)
                        {
                            if (mre.IsSet) return;
                            var state = pcg.State;
                            var t = g.Generate(pcg).Item1;
                            if (predicate(t))
                            {
                                lock (mre)
                                {
                                    if (message == null)
                                    {
                                        message = $"Example {typeof(T).Name} seed = \"{pcg.ToString(state)}\"";
                                        ret = t;
                                        mre.Set();
                                    }
                                }

                            }
                        }
                    });
                mre.Wait();
                if (output == null) throw new CsCheckException(message); else output(message);
                return ret;
            }
            else
            {
                var pcg = PCG.Parse(seed);
                var t = g.Generate(pcg).Item1;
                if (!predicate(t)) throw new CsCheckException("where clause no longer satisfied");
                return t;
            }
        }
    }

    public class FasterResult
    {
        public int Faster;
        public int Slower;
        public MedianEstimator Median;
        internal float SigmaSquared
        {
            // Binomial distribution: Mean = n p, Variance = n p q
            // in this case H0 has n = Faster + Slower, p = 0.5, and q = 0.5
            // sigmas = Abs(Faster - Mean) / Sqrt(Variance)
            //        = Sqrt((Faster - Slower)^2/(Faster + Slower))
            get
            {
                float d = Faster - Slower;
                d *= d;
                return d / (Faster + Slower);
            }
        }
        public override string ToString()
        {
            var result = $"%[-{Median.MADless * 100.0:#0}..+{Median.MADmore * 100.0:#0}]";
            result = Median.Median >= 0.0 ? (Median.Median * 100.0).ToString("#0.0") + result + " faster"
                : (Median.Median * 100.0 / (-1.0 - Median.Median)).ToString("#0.0") + result + " slower";
            return result + $", sigma={Math.Sqrt(SigmaSquared):#0.0} ({Faster:#,0} vs {Slower:#,0})";
        }
        public void Output(Action<string> output)
        {
            output(ToString());
        }
    }

    public class MedianEstimator
    {
        int N, n2 = 2, n3 = 3, n4 = 4;
        double q1, q2, q3, q4, q5;
        internal double Median => q3;
        internal double MADless => q3 - q2;
        internal double MADmore => q4 - q3;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Adjust(double p, int n1, ref int n2, int n3, double q1, ref double q2, double q3)
        {
            double d = N * p - n2;
            if ((d >= 1.0 && n3 - n2 > 1) || (d <= -1.0 && n1 - n2 < -1))
            {
                int ds = Math.Sign(d);
                double q = q2 + (double)ds / (n3 - n1) * ((n2 - n1 + ds) * (q3 - q2) / (n3 - n2) + (n3 - n2 - ds) * (q2 - q1) / (n2 - n1));
                q = q1 < q && q < q3 ? q :
                    ds == 1 ? q2 + (q3 - q2) / (n3 - n2) :
                    q2 - (q1 - q2) / (n1 - n2);
                n2 += ds;
                q2 = q;
            }
        }
        internal void Add(float s)
        {
            switch (++N)
            {
                case 1:
                    q1 = s;
                    return;
                case 2:
                    q2 = s;
                    return;
                case 3:
                    q3 = s;
                    return;
                case 4:
                    q4 = s;
                    return;
                case 5:
                    var a = new[] { q1, q2, q3, q4, s };
                    Array.Sort(a);
                    q1 = a[0];
                    q2 = a[1];
                    q3 = a[2];
                    q4 = a[3];
                    q5 = a[4];
                    return;
                default:
                    if (s < q1) q1 = s;
                    if (s < q2) n2++;
                    if (s < q3) n3++;
                    if (s < q4) n4++;
                    if (s > q5) q5 = s;
                    Adjust(0.25, 1, ref n2, n3, q1, ref q2, q3);
                    Adjust(0.50, n2, ref n3, n4, q2, ref q3, q4);
                    Adjust(0.75, n2, ref n4, N, q3, ref q4, q5);
                    return;
            }
        }
    }
}