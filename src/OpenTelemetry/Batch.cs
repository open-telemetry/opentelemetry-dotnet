// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry;

/// <summary>
/// Represents a callback action for transforming and filtering items contained
/// in a <see cref="Batch{T}"/>.
/// </summary>
/// <typeparam name="TItem">Item type.</typeparam>
/// <typeparam name="TState">State type.</typeparam>
/// <param name="item">Item being transformed.</param>
/// <param name="state">The state supplied for the transformation.</param>
/// <returns>Return <see langword="false"/> to indicate the item should be
/// removed from the <see cref="Batch{T}"/>.</returns>
public delegate bool BatchTransformationPredicate<TItem, TState>(TItem item, ref TState state)
    where TItem : class;

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
    private readonly bool rented;
    private readonly long targetCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="Batch{T}"/> struct.
    /// </summary>
    /// <param name="items">The items to store in the batch.</param>
    /// <param name="count">The number of items in the batch.</param>
    public Batch(T[] items, int count)
        : this(items, count, rented: false)
    {
    }

    internal Batch(T[] items, int count, bool rented)
    {
        Guard.ThrowIfNull(items);
        Guard.ThrowIfOutOfRange(count, min: 0, max: items.Length);

        this.items = items;
        this.rented = rented;
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

    private delegate bool BatchEnumeratorMoveNextFunc(ref Enumerator enumerator);

    /// <summary>
    /// Gets the count of items in the batch.
    /// </summary>
    public long Count { get; }

    /// <summary>
    /// Transforms and filters the items of a <see cref="Batch{T}"/> using the
    /// supplied <see cref="BatchTransformationPredicate{TItem,TState}"/> and state.
    /// </summary>
    /// <typeparam name="TState">State type.</typeparam>
    /// <param name="transformation">Transformation function. Return <see
    /// langword="false"/> to remove an item from the <see
    /// cref="Batch{T}"/>.</param>
    /// <param name="state">State to be passed into <paramref name="transformation"/>.</param>
    public void Transform<TState>(BatchTransformationPredicate<T, TState> transformation, ref TState state)
    {
        Guard.ThrowIfNull(transformation);

        if (this.Count <= 0)
        {
            return;
        }

        if (this.item != null)
        {
            Debug.Assert(
                typeof(T) != typeof(LogRecord)
                || ((LogRecord)(object)this.item).Source != LogRecord.LogRecordSource.FromSharedPool,
                "Batch contained a single item rented from the shared pool");

            // Special case for a batch of a single item

            if (!TransformItem(transformation, ref state, this.item))
            {
                Unsafe.AsRef(in this) = new Batch<T>(Array.Empty<T>(), 0, rented: false);
            }

            return;
        }

        var rentedArray = ArrayPool<T>.Shared.Rent((int)this.Count);

        var i = 0;

        if (typeof(T) == typeof(LogRecord))
        {
            foreach (var item in this)
            {
                if (TransformItem(transformation, ref state, item))
                {
                    var logRecord = (LogRecord)(object)item;
                    if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
                    {
                        logRecord.AddReference();
                    }

                    rentedArray[i++] = item;
                }
            }
        }
        else
        {
            foreach (var item in this)
            {
                if (TransformItem(transformation, ref state, item))
                {
                    rentedArray[i++] = item;
                }
            }
        }

        this.Dispose();

        Unsafe.AsRef(in this) = new Batch<T>(rentedArray, i, rented: true);

        static bool TransformItem(BatchTransformationPredicate<T, TState> transformation, ref TState state, T item)
        {
            try
            {
                return transformation(item, ref state);
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.BatchTransformationException<T>(ex);
                return true;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.circularBuffer != null)
        {
            // Note: Drain anything left in the batch and return to the pool if
            // needed.
            while (this.circularBuffer.RemovedCount < this.targetCount)
            {
                T item = this.circularBuffer.Read();
                if (typeof(T) == typeof(LogRecord))
                {
                    Enumerator.TryReturnLogRecordToPool(item);
                }
            }
        }
        else if (this.items != null && this.rented)
        {
            // Note: We don't attempt to return individual LogRecords to the
            // pool. If the batch wasn't drained fully some records may get
            // garbage collected but the pool will recreate more as needed. The
            // idea is most batches are expected to be drained during export so
            // it isn't worth the effort to track what was/was not returned.

            ArrayPool<T>.Shared.Return(this.items);

            Unsafe.AsRef(in this) = new Batch<T>(Array.Empty<T>(), 0);
        }
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="Batch{T}"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator()
    {
        return this.circularBuffer != null
            ? new Enumerator(this.circularBuffer, this.targetCount)
            : this.item != null
                ? new Enumerator(this.item)
                /* In the event someone uses default/new Batch() to create Batch we fallback to empty items mode. */
                : new Enumerator(this.items ?? Array.Empty<T>(), this.targetCount);
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
            Debug.Assert(typeof(T) != typeof(LogRecord), "T was an unexpected type");

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
            Debug.Assert(typeof(T) == typeof(LogRecord), "T was an unexpected type");

            TryReturnCurrentLogRecordToPool(ref enumerator);

            var circularBuffer = enumerator.circularBuffer;

            if (circularBuffer!.RemovedCount < enumerator.targetCount)
            {
                enumerator.current = circularBuffer.Read();
                return true;
            }

            enumerator.current = null;

            return false;
        };

        private static readonly BatchEnumeratorMoveNextFunc MoveNextArray = (ref Enumerator enumerator) =>
        {
            Debug.Assert(typeof(T) != typeof(LogRecord), "T was an unexpected type");

            var items = enumerator.items;

            if (enumerator.itemIndex < enumerator.targetCount)
            {
                enumerator.current = items![enumerator.itemIndex++];
                return true;
            }

            enumerator.current = null;
            return false;
        };

        private static readonly BatchEnumeratorMoveNextFunc MoveNextArrayLogRecord = (ref Enumerator enumerator) =>
        {
            Debug.Assert(typeof(T) == typeof(LogRecord), "T was an unexpected type");

            TryReturnCurrentLogRecordToPool(ref enumerator);

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

        internal Enumerator(T item)
        {
            this.current = item;
            this.circularBuffer = null;
            this.items = null;
            this.targetCount = -1;
            this.itemIndex = 0;
            this.moveNextFunc = MoveNextSingleItem;
        }

        internal Enumerator(CircularBuffer<T> circularBuffer, long targetCount)
        {
            this.current = null;
            this.items = null;
            this.circularBuffer = circularBuffer;
            this.targetCount = targetCount;
            this.itemIndex = 0;
            this.moveNextFunc = typeof(T) == typeof(LogRecord) ? MoveNextCircularBufferLogRecord : MoveNextCircularBuffer;
        }

        internal Enumerator(T[] items, long targetCount)
        {
            this.current = null;
            this.circularBuffer = null;
            this.items = items;
            this.targetCount = targetCount;
            this.itemIndex = 0;
            this.moveNextFunc = typeof(T) == typeof(LogRecord) ? MoveNextArrayLogRecord : MoveNextArray;
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
                    TryReturnLogRecordToPool(currentItem);
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

        internal static void TryReturnLogRecordToPool(T currentItem)
        {
            Debug.Assert(typeof(T) == typeof(LogRecord), "T was an unexpected type");
            Debug.Assert(currentItem != null, "currentItem was null");

            var logRecord = (LogRecord)(object)currentItem!;
            if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
            {
                LogRecordSharedPool.Current.Return(logRecord);
            }
        }

        private static void TryReturnCurrentLogRecordToPool(ref Enumerator enumerator)
        {
            Debug.Assert(typeof(T) == typeof(LogRecord), "T was an unexpected type");

            var currentItem = enumerator.Current;

            if (currentItem != null)
            {
                TryReturnLogRecordToPool(currentItem);
            }
        }
    }
}
