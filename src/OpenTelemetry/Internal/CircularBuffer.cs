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
    /// Lock free implementation of single-reader multi-writer circular buffer.
    /// </summary>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    internal class CircularBuffer<T>
        where T : class
    {
        private readonly int capacity;
        private readonly T[] trait;
        private long head = 0;
        private long tail = 0;

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.capacity = capacity;
            this.trait = new T[capacity];
        }

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
        /// Attempts to add the specified item to the buffer.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <returns>Returns true if the item was added to the buffer successfully; false if the buffer is full.</returns>
        public bool TryAdd(T value)
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

        public IEnumerable<T> Consume(int count)
        {
            if (count <= 0)
            {
                yield break;
            }

            count = Math.Min(count, this.Count);

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
