// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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
        => this.TryAdd(value, maxSpinCount: 0, out _);

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
        => this.TryAdd(value, maxSpinCount, out _);

    /// <summary>
    /// Attempts to add the specified item to the buffer.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <param name="maxSpinCount">The maximum allowed spin count, when set to a negative number or zero, will spin indefinitely.</param>
    /// <param name="count">
    /// When this method returns <c>true</c>, the number of items in the buffer
    /// immediately after <paramref name="value"/> was added. Items may be read
    /// concurrently, so the true count at the time of the add may have been
    /// higher but is never lower than this value. When this method returns
    /// <c>false</c>, zero.
    /// </param>
    /// <returns>
    /// Returns <c>true</c> if the item was added to the buffer successfully;
    /// <c>false</c> if the buffer is full or the spin count exceeded <paramref name="maxSpinCount"/>.
    /// </returns>
    public bool TryAdd(T value, int maxSpinCount, out long count)
    {
        Debug.Assert(value != null, "value was null");

        var spinCountDown = maxSpinCount;

        while (true)
        {
            var tailSnapshot = Volatile.Read(ref this.tail);
            var headSnapshot = Volatile.Read(ref this.head);

            if (headSnapshot - tailSnapshot >= this.Capacity)
            {
                count = 0;
                return false; // buffer is full
            }

            if (Interlocked.CompareExchange(ref this.head, headSnapshot + 1, headSnapshot) != headSnapshot)
            {
                if (maxSpinCount > 0 && spinCountDown-- == 0)
                {
                    count = 0;
                    return false; // exceeded maximum spin count
                }

                continue;
            }

            Volatile.Write(ref this.trait[headSnapshot % this.Capacity], value);

            // Re-read the tail after the add: the tail only ever grows, so this
            // count can be lower than the true count at the moment of the add but
            // never higher. While the reader is idle the tail cannot move and the
            // count is exact, which is what BatchExportProcessor relies on to
            // detect the add that brings the queue up to the export batch size.
            count = headSnapshot + 1 - Volatile.Read(ref this.tail);
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
