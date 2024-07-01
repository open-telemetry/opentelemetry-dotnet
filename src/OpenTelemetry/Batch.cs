// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry;

/// <summary>
/// Stores a batch of completed <typeparamref name="T"/> objects to be exported.
/// </summary>
/// <typeparam name="T">The type of object in the <see cref="Batch{T}"/>.</typeparam>
public readonly struct Batch<T> : IDisposable
    where T : class
{
    private readonly T? item;
    private readonly CircularBuffer<T>? circularBuffer;
    private readonly T[]? items;
    private readonly long targetCount;
    private readonly Predicate<T>? itemProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="Batch{T}"/> struct.
    /// </summary>
    /// <param name="items">The items to store in the batch.</param>
    /// <param name="count">The number of items in the batch.</param>
    public Batch(T[] items, int count)
    {
        Guard.ThrowIfNull(items);
        Guard.ThrowIfOutOfRange(count, min: 0, max: items.Length);

        this.items = items;
        this.Count = this.targetCount = count;
    }

    internal Batch(T item)
    {
        Debug.Assert(item != null, $"{nameof(item)} was null.");

        this.item = item;
        this.Count = this.targetCount = 1;
    }

    internal Batch(CircularBuffer<T> circularBuffer, int maxSize)
    {
        Debug.Assert(maxSize > 0, $"{nameof(maxSize)} should be a positive number.");
        Debug.Assert(circularBuffer != null, $"{nameof(circularBuffer)} was null.");

        this.circularBuffer = circularBuffer;
        this.Count = Math.Min(maxSize, circularBuffer!.Count);
        this.targetCount = circularBuffer.RemovedCount + this.Count;
    }

    private Batch(T? item, CircularBuffer<T>? circularBuffer, long count, long targetCount, Predicate<T> itemProcessor)
    {
        this.item = item;
        this.circularBuffer = circularBuffer;
        this.Count = count;
        this.targetCount = targetCount;
        this.itemProcessor = itemProcessor;
    }

    private delegate bool BatchEnumeratorMoveNextFunc(ref Enumerator enumerator);

    /// <summary>
    /// Gets the count of items in the batch.
    /// </summary>
    public long Count { get; }

    public Batch<T> WithProcessor(
        Predicate<T> itemProcessor)
    {
        Guard.ThrowIfNull(itemProcessor);

        var currentProcessor = this.itemProcessor;
        if (currentProcessor != null)
        {
            itemProcessor = i => currentProcessor(i) && itemProcessor(i);
        }

        return new Batch<T>(
            this.item,
            this.circularBuffer,
            this.Count,
            this.targetCount,
            itemProcessor);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.circularBuffer != null)
        {
            // Drain anything left in the batch.
            while (this.circularBuffer.RemovedCount < this.targetCount)
            {
                T item = this.circularBuffer.Read();
                if (typeof(T) == typeof(LogRecord))
                {
                    var logRecord = (LogRecord)(object)item;
                    if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
                    {
                        LogRecordSharedPool.Current.Return(logRecord);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Batch{T}"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator()
    {
        return this.circularBuffer != null
            ? new Enumerator(this.circularBuffer, this.targetCount, this.itemProcessor)
            : this.item != null
                ? new Enumerator(this.item, this.itemProcessor)
                /* In the event someone uses default/new Batch() to create Batch we fallback to empty items mode. */
                : new Enumerator(this.items ?? Array.Empty<T>(), this.targetCount, this.itemProcessor);
    }

    /// <summary>
    /// Enumerates the elements of a <see cref="Batch{T}"/>.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private static readonly BatchEnumeratorMoveNextFunc MoveNextSingleItem = (ref Enumerator enumerator) =>
        {
            if (enumerator.targetCount >= 0)
            {
                enumerator.current = null;
                return false;
            }

            enumerator.targetCount++;
            return true;
        };

        private static readonly BatchEnumeratorMoveNextFunc MoveNextCircularBuffer = (ref Enumerator enumerator) =>
        {
            var circularBuffer = enumerator.circularBuffer;

            if (circularBuffer!.RemovedCount < enumerator.targetCount)
            {
                enumerator.current = circularBuffer.Read();
                return true;
            }

            enumerator.current = null;
            return false;
        };

        private static readonly BatchEnumeratorMoveNextFunc MoveNextCircularBufferLogRecord = (ref Enumerator enumerator) =>
        {
            // Note: This type check here is to give the JIT a hint it can
            // remove all of this code when T != LogRecord
            if (typeof(T) == typeof(LogRecord))
            {
                var circularBuffer = enumerator.circularBuffer;

                var currentItem = enumerator.Current;

                if (currentItem != null)
                {
                    var logRecord = (LogRecord)(object)currentItem;
                    if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
                    {
                        LogRecordSharedPool.Current.Return(logRecord);
                    }
                }

                if (circularBuffer!.RemovedCount < enumerator.targetCount)
                {
                    enumerator.current = circularBuffer.Read();
                    return true;
                }

                enumerator.current = null;
            }

            return false;
        };

        private static readonly BatchEnumeratorMoveNextFunc MoveNextArray = (ref Enumerator enumerator) =>
        {
            var items = enumerator.items;

            if (enumerator.itemIndex < enumerator.targetCount)
            {
                enumerator.current = items![enumerator.itemIndex++];
                return true;
            }

            enumerator.current = null;
            return false;
        };

        private readonly CircularBuffer<T>? circularBuffer;
        private readonly T[]? items;
        private readonly BatchEnumeratorMoveNextFunc moveNextFunc;
        private long targetCount;
        private int itemIndex;
        [AllowNull]
        private T current;

        internal Enumerator(T item, Predicate<T>? itemProcessor)
        {
            this.current = item;
            this.circularBuffer = null;
            this.items = null;
            this.targetCount = -1;
            this.itemIndex = 0;

            this.moveNextFunc = BindMoveNextDelegate(MoveNextSingleItem, itemProcessor);
        }

        internal Enumerator(CircularBuffer<T> circularBuffer, long targetCount, Predicate<T>? itemProcessor)
        {
            this.current = null;
            this.items = null;
            this.circularBuffer = circularBuffer;
            this.targetCount = targetCount;
            this.itemIndex = 0;

            this.moveNextFunc = BindMoveNextDelegate(
                typeof(T) == typeof(LogRecord) ? MoveNextCircularBufferLogRecord : MoveNextCircularBuffer,
                itemProcessor);
        }

        internal Enumerator(T[] items, long targetCount, Predicate<T>? itemProcessor)
        {
            this.current = null;
            this.circularBuffer = null;
            this.items = items;
            this.targetCount = targetCount;
            this.itemIndex = 0;
            this.moveNextFunc = BindMoveNextDelegate(MoveNextArray, itemProcessor);
        }

        /// <inheritdoc/>
        public readonly T Current => this.current;

        /// <inheritdoc/>
        readonly object? IEnumerator.Current => this.current;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (typeof(T) == typeof(LogRecord))
            {
                var currentItem = this.current;
                if (currentItem != null)
                {
                    var logRecord = (LogRecord)(object)currentItem;
                    if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
                    {
                        LogRecordSharedPool.Current.Return(logRecord);
                    }

                    this.current = null;
                }
            }
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            return this.moveNextFunc(ref this);
        }

        /// <inheritdoc/>
        public readonly void Reset()
            => throw new NotSupportedException();

        private static BatchEnumeratorMoveNextFunc BindMoveNextDelegate(BatchEnumeratorMoveNextFunc moveNextFunc, Predicate<T>? itemProcessor)
        {
            if (itemProcessor != null)
            {
                return (ref Enumerator enumerator) =>
                {
                    while (moveNextFunc(ref enumerator))
                    {
                        if (itemProcessor(enumerator.current))
                        {
                            return true;
                        }
                    }

                    return false;
                };
            }
            else
            {
                return moveNextFunc;
            }
        }
    }
}
