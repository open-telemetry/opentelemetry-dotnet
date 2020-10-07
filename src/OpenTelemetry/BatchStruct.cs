// <copyright file="BatchStruct.cs" company="OpenTelemetry Authors">
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
    /// <typeparam name="T">The type of object in the <see cref="BatchStruct{T}"/>.</typeparam>
    public readonly struct BatchStruct<T>
        where T : struct
    {
        private readonly T item;
        private readonly CircularBufferStruct<T> circularBuffer;
        private readonly int maxSize;

        internal BatchStruct(T item)
        {
            this.item = item;
            this.circularBuffer = null;
            this.maxSize = 1;
        }

        internal BatchStruct(CircularBufferStruct<T> circularBuffer, int maxSize)
        {
            Debug.Assert(maxSize > 0, $"{nameof(maxSize)} should be a positive number.");

            this.item = default;
            this.circularBuffer = circularBuffer ?? throw new ArgumentNullException(nameof(circularBuffer));
            this.maxSize = maxSize;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="BatchStruct{T}"/>.
        /// </summary>
        /// <returns><see cref="Enumerator"/>.</returns>
        public Enumerator GetEnumerator()
        {
            return this.circularBuffer != null
                ? new Enumerator(this.circularBuffer, this.maxSize)
                : new Enumerator(this.item);
        }

        /// <summary>
        /// Enumerates the elements of a <see cref="BatchStruct{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly CircularBufferStruct<T> circularBuffer;
            private int count;

            internal Enumerator(T item)
            {
                this.Current = item;
                this.circularBuffer = null;
                this.count = -1;
            }

            internal Enumerator(CircularBufferStruct<T> circularBuffer, int maxSize)
            {
                this.Current = default;
                this.circularBuffer = circularBuffer;
                this.count = Math.Min(maxSize, circularBuffer.Count);
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
                var circularBuffer = this.circularBuffer;

                if (circularBuffer == null)
                {
                    if (this.count >= 0)
                    {
                        this.Current = default;
                        return false;
                    }

                    this.count++;
                    return true;
                }

                if (this.count > 0)
                {
                    this.Current = circularBuffer.Read();
                    this.count--;
                    return true;
                }

                this.Current = default;
                return false;
            }

            /// <inheritdoc/>
            public void Reset()
                => throw new NotSupportedException();
        }
    }
}
