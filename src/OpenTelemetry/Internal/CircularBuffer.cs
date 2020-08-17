// <copyright file="CircularBuffer.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// Lock-free implementation of single-reader multi-writer circular buffer.
    /// </summary>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    internal class CircularBuffer<T>
        where T : class
    {
        private readonly int capacity;
        private readonly T[] trait;
        private long head = 0;
        private long tail = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularBuffer{T}"/> class.
        /// </summary>
        /// <param name="capacity">The capacity of the circular buffer, must be a positive integer.</param>
        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.capacity = capacity;
            this.trait = new T[capacity];
        }

        /// <summary>
        /// Gets the capacity of the <see cref="CircularBuffer{T}"/>.
        /// </summary>
        public int Capacity
        {
            get
            {
                return this.capacity;
            }
        }

        /// <summary>
        /// Gets the number of items contained in the <see cref="CircularBuffer{T}"/>.
        /// </summary>
        public int Count
        {
            get
            {
                var tailSnapshot = this.tail;
                return (int)(this.head - tailSnapshot);
            }
        }

        /// <summary>
        /// Gets the number of items added to the <see cref="CircularBuffer{T}"/>.
        /// </summary>
        public long AddedCount
        {
            get
            {
                return this.head;
            }
        }

        /// <summary>
        /// Gets the number of items removed from the <see cref="CircularBuffer{T}"/>.
        /// </summary>
        public long RemovedCount
        {
            get
            {
                return this.tail;
            }
        }

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
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            while (true)
            {
                var tailSnapshot = this.tail;
                var headSnapshot = this.head;

                if (headSnapshot - tailSnapshot >= this.capacity)
                {
                    return false; // buffer is full
                }

                var index = (int)(headSnapshot % this.capacity);

                if (this.SwapIfNull(index, value))
                {
                    if (Interlocked.CompareExchange(ref this.head, headSnapshot + 1, headSnapshot) == headSnapshot)
                    {
                        return true;
                    }

                    this.trait[index] = null;
                }
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
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (maxSpinCount <= 0)
            {
                return this.Add(value);
            }

            var spinCountDown = maxSpinCount;

            while (true)
            {
                var tailSnapshot = this.tail;
                var headSnapshot = this.head;

                if (headSnapshot - tailSnapshot >= this.capacity)
                {
                    return false; // buffer is full
                }

                var index = (int)(headSnapshot % this.capacity);

                if (this.SwapIfNull(index, value))
                {
                    if (Interlocked.CompareExchange(ref this.head, headSnapshot + 1, headSnapshot) == headSnapshot)
                    {
                        return true;
                    }

                    this.trait[index] = null;
                }

                spinCountDown--;

                if (spinCountDown == 0)
                {
                    return false; // exceeded maximum spin count
                }
            }
        }

        /// <summary>
        /// Consumes up to <paramref name="maxCount"/> items from the queue.
        /// </summary>
        /// <param name="maxCount">
        /// The maximum number of items to be consumed, the actual number of
        /// item returned will be <c>Math.Min(maxCount, this.Count)</c>.
        /// </param>
        /// <returns>An <see cref="IEnumerable{T}"/> of items.</returns>
        /// <remarks>
        /// This function is not reentrant-safe, only one reader is allowed at any given time.
        /// </remarks>
        public IEnumerable<T> Consume(int maxCount)
        {
            if (maxCount <= 0)
            {
                yield break;
            }

            var count = Math.Min(maxCount, this.Count);

            for (int i = 0; i < count; i++)
            {
                var index = (int)(this.tail % this.capacity);
                var value = this.trait[index];
                this.trait[index] = null;
                this.tail++;
                yield return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CompareAndSwap(int index, T value, T comparand)
        {
            var result = Interlocked.CompareExchange(ref this.trait[index], value, comparand);
            return object.ReferenceEquals(result, comparand);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool SwapIfNull(int index, T value)
        {
            return this.CompareAndSwap(index, value, null);
        }
    }
}
