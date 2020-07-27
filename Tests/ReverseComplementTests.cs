﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

using Xunit;
using CsCheck;
using System.Buffers;

namespace Tests
{
    public class ReverseComplementTests
    {
        readonly Action<string> writeLine;
        public ReverseComplementTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

        //[Fact]
        public void ReverseComplement_Faster()
        {
            Check.Faster(
                () => ReverseComplementNew.RevComp.NotMain(null),
                () => ReverseComplementOld.revcomp.NotMain(null),
                threads: 1
            )
            .Output(writeLine);
        }
    }
}

namespace ReverseComplementNew
{
    public static class RevComp
    {
        const int BUFFER_SIZE = 1024 * 1024;
        static int readCount = 0, canWriteCount = 0, lastPageSize = -1;
        static int Read(Stream stream, Span<byte> span, int offset)
        {
            var bytesRead = stream.Read(span);
            return bytesRead == span.Length ? offset + bytesRead
                 : bytesRead == 0 ? offset
                 : Read(stream, span.Slice(bytesRead), offset + bytesRead);
        }

        public static void NotMain(string[] args)
        {
            var pages = new List<IMemoryOwner<byte>>();
            using var inStream = Console.OpenStandardInput();
            int bytesRead;
            do
            {
                var page = MemoryPool<byte>.Shared.Rent(BUFFER_SIZE);
                bytesRead = Read(inStream, page.Memory.Span.Slice(BUFFER_SIZE), 0);
                pages.Add(page);
            } while (bytesRead == BUFFER_SIZE);

            using var outStream = Console.OpenStandardOutput();

            int writtenCount = 0;
            while (true)
            {
                while (writtenCount == canWriteCount) Thread.MemoryBarrier();
                var page = pages[writtenCount];
                if (writtenCount + 1 == readCount && lastPageSize != -1)
                {
                    outStream.Write(page.Memory.Span.Slice(lastPageSize));
                    break;
                }
                outStream.Write(page.Memory.Span.Slice(BUFFER_SIZE));
                page.Dispose();
            }
        }
    }
}

namespace ReverseComplementOld
{
    class RevCompSequence { public List<byte[]> Pages; public int StartHeader, EndExclusive; public Thread ReverseThread; }

    public static class revcomp
    {
        const int READER_BUFFER_SIZE = 1024 * 1024;
        const byte LF = 10, GT = (byte)'>', SP = 32;
        static BlockingCollection<byte[]> readQue = new BlockingCollection<byte[]>();
        static BlockingCollection<RevCompSequence> writeQue = new BlockingCollection<RevCompSequence>();
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
            using (var stream = Console.OpenStandardInput())
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
            map[(byte)'A'] = (byte)'T';
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

        static void Writer()
        {
            using (var stream = Console.OpenStandardOutput())
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
            }
        }

        public static void NotMain(string[] args)
        {
            new Thread(Reader).Start();
            new Thread(Grouper).Start();
            Writer();
        }
    }
}