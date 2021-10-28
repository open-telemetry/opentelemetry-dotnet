// <copyright file="Batch.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry
{
    /// <summary>
    /// Stores a batch of completed <typeparamref name="T"/> objects to be exported.
    /// </summary>
    /// <typeparam name="T">The type of object in the <see cref="Batch{T}"/>.</typeparam>
    public readonly struct Batch<T> : IDisposable
        where T : class
    {
        private readonly T item;
        private readonly CircularBuffer<T> circularBuffer;
        private readonly T[] items;
        private readonly long targetCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Batch{T}"/> struct.
        /// </summary>
        /// <param name="items">The items to store in the batch.</param>
        /// <param name="count">The number of items in the batch.</param>
        public Batch(T[] items, int count)
        {
            Guard.Null(items, nameof(items));
            Guard.Range(count, nameof(count), 0, items.Length);

            this.item = null;
            this.circularBuffer = null;
            this.items = items;
            this.Count = this.targetCount = count;
        }

        internal Batch(T item)
        {
            Debug.Assert(item != null, $"{nameof(item)} was null.");

            this.item = item;
            this.circularBuffer = null;
            this.items = null;
            this.Count = this.targetCount = 1;
        }

        internal Batch(CircularBuffer<T> circularBuffer, int maxSize)
        {
            Debug.Assert(maxSize > 0, $"{nameof(maxSize)} should be a positive number.");
            Debug.Assert(circularBuffer != null, $"{nameof(circularBuffer)} was null.");

            this.item = null;
            this.items = null;
            this.circularBuffer = circularBuffer;
            this.Count = Math.Min(maxSize, circularBuffer.Count);
            this.targetCount = circularBuffer.RemovedCount + this.Count;
        }

        /// <summary>
        /// Gets the count of items in the batch.
        /// </summary>
        public long Count { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.circularBuffer != null)
            {
                // Drain anything left in the batch.
                while (this.circularBuffer.RemovedCount < this.targetCount)
                {
                    this.circularBuffer.Read();
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
                ? new Enumerator(this.circularBuffer, this.targetCount)
                : this.items != null
                    ? new Enumerator(this.items, this.targetCount)
                    : new Enumerator(this.item);
        }

        /// <summary>
        /// Enumerates the elements of a <see cref="Batch{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly CircularBuffer<T> circularBuffer;
            private readonly T[] items;
            private long targetCount;
            private int itemIndex;

            internal Enumerator(T item)
            {
                this.Current = item;
                this.circularBuffer = null;
                this.items = null;
                this.targetCount = -1;
                this.itemIndex = 0;
            }

            internal Enumerator(CircularBuffer<T> circularBuffer, long targetCount)
            {
                this.Current = null;
                this.items = null;
                this.circularBuffer = circularBuffer;
                this.targetCount = targetCount;
                this.itemIndex = 0;
            }

            internal Enumerator(T[] items, long targetCount)
            {
                this.Current = null;
                this.circularBuffer = null;
                this.items = items;
                this.targetCount = targetCount;
                this.itemIndex = 0;
            }

            /// <inheritdoc/>
            public T Current { get; private set; }

            /// <inheritdoc/>
            object IEnumerator.Current => this.Current;

            /// <inheritdoc/>
            public void Dispose()
            {
            }

            /// <inheritdoc/>
            public bool MoveNext()
            {
                if (this.items != null)
                {
                    if (this.itemIndex < this.targetCount)
                    {
                        this.Current = this.items[this.itemIndex];
                        this.itemIndex++;
                        return true;
                    }

                    this.Current = null;
                    return false;
                }

                var circularBuffer = this.circularBuffer;

                if (circularBuffer == null)
                {
                    if (this.targetCount >= 0)
                    {
                        this.Current = null;
                        return false;
                    }

                    this.targetCount++;
                    return true;
                }

                if (circularBuffer.RemovedCount < this.targetCount)
                {
                    this.Current = circularBuffer.Read();
                    return true;
                }

                this.Current = null;
                return false;
            }

            /// <inheritdoc/>
            public void Reset()
                => throw new NotSupportedException();
        }
    }
}
