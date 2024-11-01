// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Internal;

internal readonly struct PooledList<T> : IEnumerable<T>, ICollection
{
    public static int LastAllocatedSize = 64;

    private readonly T[] buffer;

    private PooledList(T[] buffer, int count)
    {
        this.buffer = buffer;
        this.Count = count;
    }

    public int Count { get; }

    public bool IsEmpty => this.Count == 0;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    public ref T this[int index]
    {
        get => ref this.buffer[index];
    }

    public static PooledList<T> Create()
    {
        return new PooledList<T>(ArrayPool<T>.Shared.Rent(LastAllocatedSize), 0);
    }

    public static void Add(ref PooledList<T> list, T item)
    {
        Guard.ThrowIfNull(list.buffer);

        var buffer = list.buffer;

        if (list.Count >= buffer.Length)
        {
            LastAllocatedSize = buffer.Length * 2;
            var previousBuffer = buffer;

            buffer = ArrayPool<T>.Shared.Rent(LastAllocatedSize);

            var span = previousBuffer.AsSpan();
            span.CopyTo(buffer);
            ArrayPool<T>.Shared.Return(previousBuffer);
        }

        buffer[list.Count] = item;
        list = new PooledList<T>(buffer, list.Count + 1);
    }

    public static void Clear(ref PooledList<T> list)
    {
        list = new PooledList<T>(list.buffer, 0);
    }

    public void Return()
    {
        var buffer = this.buffer;
        if (buffer != null)
        {
            ArrayPool<T>.Shared.Return(buffer);
        }
    }

    void ICollection.CopyTo(Array array, int index)
    {
        Array.Copy(this.buffer, 0, array, index, this.Count);
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(in this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(in this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(in this);
    }

    public struct Enumerator : IEnumerator<T>, IEnumerator
    {
        private readonly T[] buffer;
        private readonly int count;
        private int index;
        [AllowNull]
        private T current;

        public Enumerator(in PooledList<T> list)
        {
            this.buffer = list.buffer;
            this.count = list.Count;
            this.index = 0;
            this.current = default;
        }

        public T Current => this.current;

        object? IEnumerator.Current => this.Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (this.index < this.count)
            {
                this.current = this.buffer[this.index++];
                return true;
            }

            this.index = this.count + 1;
            this.current = default;
            return false;
        }

        void IEnumerator.Reset()
        {
            this.index = 0;
            this.current = default;
        }
    }
}
