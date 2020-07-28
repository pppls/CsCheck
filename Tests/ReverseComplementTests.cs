﻿namespace Tests
{
    using System;
    using System.IO;
    using Xunit;
    using CsCheck;

    public class ReverseComplementTests
    {
        readonly Action<string> writeLine;
        public ReverseComplementTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

        [Fact]
        public void ReverseComplement_Faster()
        {
            if (!File.Exists(Utils.Fasta.Filename)) Utils.Fasta.NotMain(new[] { "25000000" });

            Check.Faster(
                () => ReverseComplementNew.RevComp.NotMain(null),
                () => ReverseComplementOld.RevComp.NotMain(null),
                threads: 1, timeout: 300_000
            )
            .Output(writeLine);
        }
    }
}

namespace ReverseComplementNew
{
    using System;
    using System.IO;
    using System.Threading;

    public static class RevComp
    {
        public static int NotMain(string[] args)
        {
            const int PAGE_SIZE = 1024 * 1024;
            var pages = new byte[1024][];
            int readCount = 0, canWriteCount = 0, lastPageID = -1, lastPageSize = PAGE_SIZE;
            new Thread(() =>
            {
                static int Read(Stream stream, byte[] bytes, int offset)
                {
                    var bytesRead = stream.Read(bytes, offset, PAGE_SIZE - offset);
                    return bytesRead + offset == PAGE_SIZE ? PAGE_SIZE
                         : bytesRead == 0 ? offset
                         : Read(stream, bytes, offset + bytesRead);
                }
                using var inStream = File.OpenRead(Utils.Fasta.Filename);//Console.OpenStandardInput();
                while(true)
                {
                    var page = new byte[PAGE_SIZE];
                    pages[readCount] = page;
                    lastPageSize = Read(inStream, page, 0);
                    if(lastPageSize != PAGE_SIZE)
                    {
                        lastPageID = readCount++;
                        break;
                    }
                    readCount++;
                }
            }).Start();

            const byte LF = (byte)'\n', GT = (byte)'>';
            new Thread(() =>
            {
                var map = new byte[256];
                for (int b = 0; b < map.Length; b++) map[b] = (byte)b;
                map['A'] = map['a'] = (byte)'T';
                map['B'] = map['b'] = (byte)'V';
                map['C'] = map['c'] = (byte)'G';
                map['D'] = map['d'] = (byte)'H';
                map['G'] = map['g'] = (byte)'C';
                map['H'] = map['h'] = (byte)'D';
                map['K'] = map['k'] = (byte)'M';
                map['M'] = map['m'] = (byte)'K';
                map['R'] = map['r'] = (byte)'Y';
                map['T'] = map['t'] = (byte)'A';
                map['V'] = map['v'] = (byte)'B';
                map['Y'] = map['y'] = (byte)'R';

                void Reverse(int loPageID, int lo, int endPageID, int hi, Thread previous)
                {
                    var hiPageID = endPageID;
                    var loPage = pages[loPageID];
                    var hiPage = pages[hiPageID];
                    while (true)
                    {
                        if (lo == PAGE_SIZE)
                        {
                            lo = 0;
                            loPage = pages[++loPageID];
                            if (previous == null || !previous.IsAlive) canWriteCount = loPageID;
                        }
                        if (hi == -1)
                        {
                            hi = PAGE_SIZE - 1;
                            hiPage = pages[--hiPageID];
                        }
                        if (loPageID > hiPageID || (loPageID == hiPageID && lo > hi)) break;
                        ref var loValue = ref loPage[lo];
                        ref var hiValue = ref hiPage[hi];
                        if (loValue == LF)
                        {
                            lo++;
                            if (hiValue == LF) hi--;
                        }
                        else if (hiValue == LF)
                        {
                            hi--;
                        }
                        else
                        {
                            var swap = map[loValue];
                            loValue = map[hiValue];
                            hiValue = swap;
                            lo++;
                            hi--;
                        }
                    }
                    previous?.Join();
                    canWriteCount = endPageID;
                }

                int pageID = 0, index = 0; Thread previous = null;
                while (true)
                {
                    while (true) // skip header
                    {
                        while (pageID == readCount) Thread.MemoryBarrier();
                        int i = Array.IndexOf(pages[pageID], LF, index);
                        if (i != -1)
                        {
                            index = i + 1;
                            break;
                        }
                        index = 0;
                        pageID++;
                    }
                    var loPageID = pageID;
                    var lo = index;
                    while (true)
                    {
                        while (pageID == readCount) Thread.MemoryBarrier();
                        var thisPageSize = pageID == lastPageID ? lastPageSize : PAGE_SIZE;
                        var i = Array.IndexOf(pages[pageID], GT, index, thisPageSize - index);
                        if (i != -1)
                        {
                            index = i;
                            var loPageIDCopy = loPageID;
                            var loCopy = lo;
                            var hiPageID = pageID;
                            var hi = index - 1;
                            var prior = previous;
                            (previous = new Thread(() => Reverse(loPageIDCopy, loCopy, hiPageID, hi, prior))).Start();
                            break;
                        }
                        else if (pageID == lastPageID)
                        {
                            Reverse(loPageID, lo, pageID, lastPageSize - 1, previous);
                            canWriteCount = readCount;
                            return;
                        }
                        pageID++;
                        index = 0;
                    }
                }
            }).Start();

            using var outStream = new Utils.HashStream(); //Console.OpenStandardOutput();
            int writtenCount = 0;
            while (true)
            {
                while (writtenCount == canWriteCount) Thread.MemoryBarrier();
                var page = pages[writtenCount];
                if (writtenCount++ == lastPageID)
                {
                    outStream.Write(page, 0, lastPageSize);
                    return outStream.GetHashCode();
                }
                outStream.Write(page, 0, PAGE_SIZE);
            }
        }
    }
}

namespace ReverseComplementOld
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Collections.Concurrent;
    using System.Threading;

    class RevCompSequence { public List<byte[]> Pages; public int StartHeader, EndExclusive; public Thread ReverseThread; }

    public static class RevComp
    {
        const int READER_BUFFER_SIZE = 1024 * 1024;
        const byte LF = 10, GT = (byte)'>', SP = 32;
        static BlockingCollection<byte[]> readQue;
        static BlockingCollection<RevCompSequence> writeQue;
        static byte[] map;

        static int read(Stream stream, byte[] buffer, int offset, int count)
        {
            var bytesRead = stream.Read(buffer, offset, count);
            return bytesRead == count ? offset + count
                 : bytesRead == 0 ? offset
                 : read(stream, buffer, offset + bytesRead, count - bytesRead);
        }
        static void Reader()
        {
            using (var stream = File.OpenRead(Utils.Fasta.Filename))//Console.OpenStandardInput())
            {
                int bytesRead;
                do
                {
                    var buffer = new byte[READER_BUFFER_SIZE];
                    bytesRead = read(stream, buffer, 0, READER_BUFFER_SIZE);
                    readQue.Add(buffer);
                } while (bytesRead == READER_BUFFER_SIZE);
                readQue.CompleteAdding();
            }
        }

        static bool tryTake<T>(BlockingCollection<T> q, out T t) where T : class
        {
            t = null;
            while (!q.IsCompleted && !q.TryTake(out t)) Thread.SpinWait(0);
            return t != null;
        }

        static void Grouper()
        {
            // Set up complements map
            map = new byte[256];
            for (byte b = 0; b < 255; b++) map[b] = b;
            map[(byte)'A'] = (byte)'T'; map[(byte)'a'] = (byte)'T';
            map[(byte)'B'] = (byte)'V';
            map[(byte)'C'] = (byte)'G';
            map[(byte)'D'] = (byte)'H';
            map[(byte)'G'] = (byte)'C';
            map[(byte)'H'] = (byte)'D';
            map[(byte)'K'] = (byte)'M';
            map[(byte)'M'] = (byte)'K';
            map[(byte)'R'] = (byte)'Y';
            map[(byte)'T'] = (byte)'A';
            map[(byte)'V'] = (byte)'B';
            map[(byte)'Y'] = (byte)'R';
            map[(byte)'a'] = (byte)'T';
            map[(byte)'b'] = (byte)'V';
            map[(byte)'c'] = (byte)'G';
            map[(byte)'d'] = (byte)'H';
            map[(byte)'g'] = (byte)'C';
            map[(byte)'h'] = (byte)'D';
            map[(byte)'k'] = (byte)'M';
            map[(byte)'m'] = (byte)'K';
            map[(byte)'r'] = (byte)'Y';
            map[(byte)'t'] = (byte)'A';
            map[(byte)'v'] = (byte)'B';
            map[(byte)'y'] = (byte)'R';

            var startHeader = 0;
            var i = 0;
            bool afterFirst = false;
            var data = new List<byte[]>();
            byte[] bytes;
            while (tryTake(readQue, out bytes))
            {
                data.Add(bytes);
                while ((i = Array.IndexOf<byte>(bytes, GT, i + 1)) != -1)
                {
                    var sequence = new RevCompSequence
                    {
                        Pages = data
                        ,
                        StartHeader = startHeader,
                        EndExclusive = i
                    };
                    if (afterFirst)
                        (sequence.ReverseThread = new Thread(() => Reverse(sequence))).Start();
                    else
                        afterFirst = true;
                    writeQue.Add(sequence);
                    startHeader = i;
                    data = new List<byte[]> { bytes };
                }
            }
            i = Array.IndexOf<byte>(data[data.Count - 1], 0, 0);
            var lastSequence = new RevCompSequence
            {
                Pages = data
                ,
                StartHeader = startHeader,
                EndExclusive = i == -1 ? data[data.Count - 1].Length : i
            };
            Reverse(lastSequence);
            writeQue.Add(lastSequence);
            writeQue.CompleteAdding();
        }

        static void Reverse(RevCompSequence sequence)
        {
            var startPageId = 0;
            var startBytes = sequence.Pages[0];
            var startIndex = sequence.StartHeader;

            // Skip header line
            while ((startIndex = Array.IndexOf<byte>(startBytes, LF, startIndex)) == -1)
            {
                startBytes = sequence.Pages[++startPageId];
                startIndex = 0;
            }

            var endPageId = sequence.Pages.Count - 1;
            var endIndex = sequence.EndExclusive - 1;
            if (endIndex == -1) endIndex = sequence.Pages[--endPageId].Length - 1;
            var endBytes = sequence.Pages[endPageId];

            // Swap in place across pages
            do
            {
                var startByte = startBytes[startIndex];
                if (startByte < SP)
                {
                    if (++startIndex == startBytes.Length)
                    {
                        startBytes = sequence.Pages[++startPageId];
                        startIndex = 0;
                    }
                    if (startIndex == endIndex && startPageId == endPageId) break;
                    startByte = startBytes[startIndex];
                }
                var endByte = endBytes[endIndex];
                if (endByte < SP)
                {
                    if (--endIndex == -1)
                    {
                        endBytes = sequence.Pages[--endPageId];
                        endIndex = endBytes.Length - 1;
                    }
                    if (startIndex == endIndex && startPageId == endPageId) break;
                    endByte = endBytes[endIndex];
                }

                startBytes[startIndex] = map[endByte];
                endBytes[endIndex] = map[startByte];

                if (++startIndex == startBytes.Length)
                {
                    startBytes = sequence.Pages[++startPageId];
                    startIndex = 0;
                }
                if (--endIndex == -1)
                {
                    endBytes = sequence.Pages[--endPageId];
                    endIndex = endBytes.Length - 1;
                }
            } while (startPageId < endPageId || (startPageId == endPageId && startIndex < endIndex));
            if (startIndex == endIndex) startBytes[startIndex] = map[startBytes[startIndex]];
        }

        static int Writer()
        {
            using (var stream = new Utils.HashStream())// Console.OpenStandardOutput())
            {
                bool first = true;
                RevCompSequence sequence;
                while (tryTake(writeQue, out sequence))
                {
                    var startIndex = sequence.StartHeader;
                    var pages = sequence.Pages;
                    if (first)
                    {
                        Reverse(sequence);
                        first = false;
                    }
                    else
                    {
                        sequence.ReverseThread?.Join();
                    }
                    for (int i = 0; i < pages.Count - 1; i++)
                    {
                        var bytes = pages[i];
                        stream.Write(bytes, startIndex, bytes.Length - startIndex);
                        startIndex = 0;
                    }
                    stream.Write(pages[pages.Count - 1], startIndex, sequence.EndExclusive - startIndex);
                }
                return stream.GetHashCode();
            }
        }

        public static int NotMain(string[] args)
        {
            readQue = new BlockingCollection<byte[]>();
            writeQue = new BlockingCollection<RevCompSequence>();
            new Thread(Reader).Start();
            new Thread(Grouper).Start();
            return Writer();
        }
    }
}

namespace Utils
{
    using System;
    using System.IO;
    using System.Text;
    using System.Buffers;
    using System.Threading;
    using System.Runtime.CompilerServices;

    public class HashStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set { } }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException();
        public override long Seek(long offset, SeekOrigin origin) => 0;
        public override void SetLength(long value) { }
        public override void Write(byte[] buffer, int offset, int count)
        {
            int h = hashCode;
            foreach (byte b in buffer.AsSpan(offset, count))
                h = (h ^ b) * 16777619;
            hashCode = h;
        }
        int hashCode = -2128831035;
        public override int GetHashCode() => hashCode;
    }

    public class Fasta
    {
        public const string Filename = "input25000000.txt";
        const int Width = 60;
        const int Width1 = 61;
        const int LinesPerBlock = 2048;
        const int BlockSize = Width * LinesPerBlock;
        const int BlockSize1 = Width1 * LinesPerBlock;
        const int IM = 139968;
        const float FIM = 1F / 139968F;
        const int IA = 3877;
        const int IC = 29573;
        const int SEED = 42;
        static readonly ArrayPool<byte> bytePool = ArrayPool<byte>.Shared;
        static readonly ArrayPool<int> intPool = ArrayPool<int>.Shared;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte[] Bytes(int i, int[] rnds, float[] ps, byte[] vs)
        {
            var a = bytePool.Rent(BlockSize1);
            var s = a.AsSpan(0, i);
            for (i = 1; i < s.Length; i++)
            {
                var p = rnds[i] * FIM;
                int j = 0;
                while (ps[j] < p) j++;
                s[i] = vs[j];
            }
            return a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int[] Rnds(int i, int j, ref int seed)
        {
            var a = intPool.Rent(BlockSize1);
            var s = a.AsSpan(0, i);
            s[0] = j;
            for (i = 1, j = Width; i < s.Length; i++)
            {
                if (j-- == 0)
                {
                    j = Width;
                    s[i] = IM * 3 / 2;
                }
                else
                {
                    s[i] = seed = (seed * IA + IC) % IM;
                }
            }
            return a;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int WriteRandom(int n, int offset, int seed, byte[] vs, float[] ps,
            Tuple<byte[], int>[] blocks)
        {
            // make cumulative
            var total = ps[0];
            for (int i = 1; i < ps.Length; i++)
                ps[i] = total += ps[i];

            void create(object o)
            {
                var rnds = (int[])o;
                blocks[rnds[0]] =
                    Tuple.Create(Bytes(BlockSize1, rnds, ps, vs), BlockSize1);
                intPool.Return(rnds);
            }

            var createDel = (WaitCallback)create;

            for (int i = offset; i < offset + (n - 1) / BlockSize; i++)
            {
                ThreadPool.QueueUserWorkItem(createDel,
                    Rnds(BlockSize1, i, ref seed));
            }

            var remaining = (n - 1) % BlockSize + 1;
            var l = remaining + (remaining - 1) / Width + 1;
            ThreadPool.QueueUserWorkItem(o =>
            {
                var rnds = (int[])o;
                blocks[rnds[0]] = Tuple.Create(Bytes(l, rnds, ps, vs), l);
                intPool.Return(rnds);
            }, Rnds(l, offset + (n - 1) / BlockSize, ref seed));

            return seed;
        }

        public static void NotMain(string[] args)
        {
            int n = args.Length == 0 ? 1000 : int.Parse(args[0]);
            using var o = File.Create(Filename);//Console.OpenStandardOutput();
            var blocks = new Tuple<byte[], int>[
                (3 * n - 1) / BlockSize + (5 * n - 1) / BlockSize + 3];

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var seed = WriteRandom(3 * n, 0, SEED,
                    new[] { (byte)'a', (byte)'c', (byte)'g', (byte)'t',
                    (byte)'B', (byte)'D', (byte)'H', (byte)'K', (byte)'M',
                    (byte)'N', (byte)'R', (byte)'S', (byte)'V', (byte)'W',
                    (byte)'Y', (byte)'\n' },
                    new[] { 0.27F,0.12F,0.12F,0.27F,0.02F,0.02F,0.02F,0.02F,0.02F,
                    0.02F,0.02F,0.02F,0.02F,0.02F,0.02F,1.00F }, blocks);

                WriteRandom(5 * n, (3 * n - 1) / BlockSize + 2, seed,
                    new byte[] { (byte)'a', (byte)'c', (byte)'g', (byte)'t',
                    (byte)'\n' },
                    new[] { 0.3029549426680F, 0.1979883004921F,
                        0.1975473066391F, 0.3015094502008F,
                        1.0F }, blocks);
            });

            o.Write(Encoding.ASCII.GetBytes(">ONE Homo sapiens alu"), 0, 21);
            var table = Encoding.ASCII.GetBytes(
                "GGCCGGGCGCGGTGGCTCACGCCTGTAATCCCAGCACTTTGG" +
                "GAGGCCGAGGCGGGCGGATCACCTGAGGTCAGGAGTTCGAGA" +
                "CCAGCCTGGCCAACATGGTGAAACCCCGTCTCTACTAAAAAT" +
                "ACAAAAATTAGCCGGGCGTGGTGGCGCGCGCCTGTAATCCCA" +
                "GCTACTCGGGAGGCTGAGGCAGGAGAATCGCTTGAACCCGGG" +
                "AGGCGGAGGTTGCAGTGAGCCGAGATCGCGCCACTGCACTCC" +
                "AGCCTGGGCGACAGAGCGAGACTCCGTCTCAAAAA");
            var linesPerBlock = (LinesPerBlock / 287 + 1) * 287;
            var repeatedBytes = bytePool.Rent(Width1 * linesPerBlock);
            for (int i = 0; i <= linesPerBlock * Width - 1; i++)
                repeatedBytes[1 + i + i / Width] = table[i % 287];
            for (int i = 0; i <= (Width * linesPerBlock - 1) / Width; i++)
                repeatedBytes[i * Width1] = (byte)'\n';
            for (int i = 1; i <= (2 * n - 1) / (Width * linesPerBlock); i++)
                o.Write(repeatedBytes, 0, Width1 * linesPerBlock);
            var remaining = (2 * n - 1) % (Width * linesPerBlock) + 1;
            o.Write(repeatedBytes, 0, remaining + (remaining - 1) / Width + 1);
            bytePool.Return(repeatedBytes);
            o.Write(Encoding.ASCII.GetBytes("\n>TWO IUB ambiguity codes"), 0, 25);

            blocks[(3 * n - 1) / BlockSize + 1] = Tuple.Create
                (Encoding.ASCII.GetBytes("\n>THREE Homo sapiens frequency"), 30);

            for (int i = 0; i < blocks.Length; i++)
            {
                Tuple<byte[], int> t;
                while ((t = blocks[i]) == null) Thread.Sleep(0);
                t.Item1[0] = (byte)'\n';
                o.Write(t.Item1, 0, t.Item2);
                if (t.Item2 == BlockSize1) bytePool.Return(t.Item1);
            }

            o.WriteByte((byte)'\n');
        }
    }
}