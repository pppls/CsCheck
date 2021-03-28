﻿using System;
using System.Linq;
using System.Collections.Generic;
using CsCheck;
using Xunit;
using System.Runtime.CompilerServices;
using System.Collections;

namespace Tests
{
    public class SlimCollectionsTests
    {
        readonly Action<string> writeLine;
        public SlimCollectionsTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;
        [Fact]
        public void ListSlim_ModelBased()
        {
            Gen.Int.Array.Select(a =>
            {
                var l = new ListSlim<int>(a.Length);
                foreach (var i in a) l.Add(i);
                return (l, a.ToList());
            })
            .SampleModelBased(
                Gen.Int.Operation<ListSlim<int>, List<int>>((ls, l, i) => {
                    ls.Add(i);
                    l.Add(i);
                })
            );
        }

        [Fact]
        public void ListSlim_Concurrency()
        {
            Gen.Byte.Array.Select(a =>
            {
                var l = new ListSlim<byte>(a.Length);
                foreach (var i in a) l.Add(i);
                return l;
            })
            .SampleConcurrent(
                Gen.Byte.Operation<ListSlim<byte>>((l, i) => { lock (l) l.Add(i); }),
                Gen.Int.NonNegative.Operation<ListSlim<byte>>((l, i) => { if (i < l.Count) { var _ = l[i]; } }),
                Gen.Int.NonNegative.Select(Gen.Byte).Operation<ListSlim<byte>>((l, t) => { if (t.V0 < l.Count) l[t.V0] = t.V1; }),
                Gen.Operation<ListSlim<byte>>(l => l.ToArray())
            );
        }

        [Fact]
        public void ListSlim_Faster()
        {
            Gen.Byte.Array
            .Faster(
                t =>
                {
                    var d = new ListSlim<byte>();
                    for (int i = 0; i < t.Length; i++)
                        d.Add(t[i]);
                    return d.Count;
                },
                t =>
                {
                    var d = new List<byte>();
                    for (int i = 0; i < t.Length; i++)
                        d.Add(t[i]);
                    return d.Count;
                },
                repeat: 50
            ).Output(writeLine);
        }

        [Fact(Skip ="WIP")]
        public void SetSlim_ModelBased()
        {
            Gen.Int.Array.Select(a =>
            {
                var l = new SetSlim<int>();
                foreach (var i in a) l.Add(i);
                return (l, new HashSet<int>(a));
            })
            .SampleModelBased(
                Gen.Int.Operation<SetSlim<int>, HashSet<int>>((ls, l, i) => {
                    ls.Add(i);
                    l.Add(i);
                })
            );
        }
    }


    public class ListSlim<T> : IReadOnlyList<T>
    {
        static readonly T[] empty = Array.Empty<T>();
        T[] entries;
        int count;
        public ListSlim() => entries = empty;
        public ListSlim(int capacity) => entries = new T[capacity];
        public int Count => count;
        public T this[int i]
        {
            get => entries[i];
            set => entries[i] = value;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddWithResize(T item)
        {
            int c = count;
            if (c == 0) entries = new T[4];
            else
            {
                var newEntries = new T[c * 2];
                Array.Copy(entries, 0, newEntries, 0, c);
                entries = newEntries;
            }
            entries[c] = item;
            count = c + 1;
        }

        public void Add(T item)
        {
            T[] e = entries;
            int c = count;
            if ((uint)c < (uint)e.Length)
            {
                e[c] = item;
                count = c + 1;
            }
            else
            {
                AddWithResize(item);
            }
        }

        public T[] ToArray()
        {
            int c = count;
            var a = new T[c];
            Array.Copy(entries, 0, a, 0, c);
            return a;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
                yield return entries[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }


    public class SetSlim<T> : IReadOnlyCollection<T> where T : IEquatable<T>
    {
        static class SetSlimHolder { internal static Entry[] Initial = new Entry[1]; }
        struct Entry { internal int Bucket; internal int Next; internal T Item; }
        int count;
        Entry[] entries;
        public SetSlim()
        {
            count = 0;
            entries = SetSlimHolder.Initial;
        }
        public SetSlim(int capacity)
        {
            count = 0;
            if (capacity < 2) capacity = 2;
            entries = new Entry[PowerOf2(capacity)];
        }
        static int PowerOf2(int capacity)
        {
            if ((capacity & (capacity - 1)) == 0) return capacity;
            int i = 2;
            while (i < capacity) i <<= 1;
            return i;
        }
        void Resize()
        {
            var oldEntries = entries;
            var newEntries = new Entry[oldEntries.Length * 2];
            int i = oldEntries.Length;
            while (i-- > 0)
            {
                newEntries[i].Item = oldEntries[i].Item;
                var bi = newEntries[i].Item!.GetHashCode() & (newEntries.Length - 1);
                newEntries[i].Next = newEntries[bi].Bucket - 1;
                newEntries[i].Bucket = i + 1;
            }
            entries = newEntries;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        int AddItem(T item, int hashCode)
        {
            var i = count;
            if (i == 0 && entries.Length == 1)
                entries = new Entry[2];
            else if (i == entries.Length)
                Resize();
            var ent = entries;
            ent[i].Item = item;
            var bucketIndex = hashCode & (ent.Length - 1);
            ent[i].Next = ent[bucketIndex].Bucket - 1;
            ent[bucketIndex].Bucket = i + 1;
            count = i + 1;
            return i;
        }

        public int Add(T item)
        {
            var ent = entries;
            var hashCode = item.GetHashCode();
            var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
            while (i >= 0 && !item.Equals(ent[i].Item)) i = ent[i].Next;
            return i >= 0 ? i : AddItem(item, hashCode);
        }

        public int IndexOf(T item)
        {
            var ent = entries;
            var hashCode = item.GetHashCode();
            var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
            while (i >= 0 && !item.Equals(ent[i].Item)) i = ent[i].Next;
            return i;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
                yield return entries[i].Item;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T this[int i] => entries[i].Item;
        public int Count => count;
    }
}