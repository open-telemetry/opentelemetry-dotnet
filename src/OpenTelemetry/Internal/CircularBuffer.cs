// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Internal;

/// <summary>
/// Lock-free implementation of single-reader multi-writer circular buffer.
/// </summary>
/// <typeparam name="T">The type of the underlying value.</typeparam>
internal sealed class CircularBuffer<T>
    where T : class
{
    private readonly T?[] trait;
    private long head;
    private long tail;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircularBuffer{T}"/> class.
    /// </summary>
    /// <param name="capacity">The capacity of the circular buffer, must be a positive integer.</param>
    public CircularBuffer(int capacity)
    {
        Guard.ThrowIfOutOfRange(capacity, min: 1);

        this.Capacity = capacity;
        this.trait = new T[capacity];
    }

    /// <summary>
    /// Gets the capacity of the <see cref="CircularBuffer{T}"/>.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the number of items contained in the <see cref="CircularBuffer{T}"/>.
    /// </summary>
    public int Count
    {
        get
        {
            var tailSnapshot = Volatile.Read(ref this.tail);
            return (int)(Volatile.Read(ref this.head) - tailSnapshot);
        }
    }

    /// <summary>
    /// Gets the number of items added to the <see cref="CircularBuffer{T}"/>.
    /// </summary>
    public long AddedCount => Volatile.Read(ref this.head);

    /// <summary>
    /// Gets the number of items removed from the <see cref="CircularBuffer{T}"/>.
    /// </summary>
    public long RemovedCount => Volatile.Read(ref this.tail);

    /// <summary>
    /// Adds the specified item to the buffer.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>
    /// Returns <c>true</c> if the item was added to the buffer successfully;
    /// <c>false</c> if the buffer is full.
    /// </returns>
    public bool Add(T value)
    {
        Guard.ThrowIfNull(value);

        while (true)
        {
            var tailSnapshot = Volatile.Read(ref this.tail);
            var headSnapshot = Volatile.Read(ref this.head);

            if (headSnapshot - tailSnapshot >= this.Capacity)
            {
                return false; // buffer is full
            }

            if (Interlocked.CompareExchange(ref this.head, headSnapshot + 1, headSnapshot) != headSnapshot)
            {
                continue;
            }

            Volatile.Write(ref this.trait[headSnapshot % this.Capacity], value);

            return true;
        }
    }

    /// <summary>
    /// Attempts to add the specified item to the buffer.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <param name="maxSpinCount">The maximum allowed spin count, when set to a negative number or zero, will spin indefinitely.</param>
    /// <returns>
    /// Returns <c>true</c> if the item was added to the buffer successfully;
    /// <c>false</c> if the buffer is full or the spin count exceeded <paramref name="maxSpinCount"/>.
    /// </returns>
    public bool TryAdd(T value, int maxSpinCount)
    {
        if (maxSpinCount <= 0)
        {
            return this.Add(value);
        }

        Guard.ThrowIfNull(value);

        var spinCountDown = maxSpinCount;

        while (true)
        {
            var tailSnapshot = Volatile.Read(ref this.tail);
            var headSnapshot = Volatile.Read(ref this.head);

            if (headSnapshot - tailSnapshot >= this.Capacity)
            {
                return false; // buffer is full
            }

            if (Interlocked.CompareExchange(ref this.head, headSnapshot + 1, headSnapshot) != headSnapshot)
            {
                if (spinCountDown-- == 0)
                {
                    return false; // exceeded maximum spin count
                }

                continue;
            }

            Volatile.Write(ref this.trait[headSnapshot % this.Capacity], value);

            return true;
        }
    }

    /// <summary>
    /// Reads an item from the <see cref="CircularBuffer{T}"/>.
    /// </summary>
    /// <remarks>
    /// This function is not reentrant-safe, only one reader is allowed at any given time.
    /// Warning: There is no bounds check in this method. Do not call unless you have verified Count > 0.
    /// </remarks>
    /// <returns>Item read.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Read()
    {
        var tail = Volatile.Read(ref this.tail);
        var index = (int)(tail % this.Capacity);
        while (true)
        {
            var previous = Interlocked.Exchange(ref this.trait[index], null);
            if (previous == null)
            {
                // If we got here it means a writer isn't done.
                continue;
            }

            Volatile.Write(ref this.tail, tail + 1);
            return previous;
        }
    }
}
