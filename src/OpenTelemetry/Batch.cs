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
using OpenTelemetry.Metrics;

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
        private readonly T[] metrics;
        private readonly long targetCount;

        internal Batch(T item)
        {
            Guard.Null(item, nameof(item));

            this.item = item;
            this.circularBuffer = null;
            this.metrics = null;
            this.targetCount = 1;
        }

        internal Batch(CircularBuffer<T> circularBuffer, int maxSize)
        {
            Debug.Assert(maxSize > 0, $"{nameof(maxSize)} should be a positive number.");
            Guard.Null(circularBuffer, nameof(circularBuffer));

            this.item = null;
            this.metrics = null;
            this.circularBuffer = circularBuffer;
            this.targetCount = circularBuffer.RemovedCount + Math.Min(maxSize, circularBuffer.Count);
        }

        internal Batch(T[] metrics, int maxSize)
        {
            Debug.Assert(maxSize > 0, $"{nameof(maxSize)} should be a positive number.");
            Guard.Null(metrics, nameof(metrics));

            this.item = null;
            this.circularBuffer = null;
            this.metrics = metrics;
            this.targetCount = maxSize;
        }

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
                : this.metrics != null
                ? new Enumerator(this.metrics, this.targetCount)
                : new Enumerator(this.item);
        }

        /// <summary>
        /// Enumerates the elements of a <see cref="Batch{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly CircularBuffer<T> circularBuffer;
            private readonly T[] metrics;
            private long targetCount;
            private int metricIndex;

            internal Enumerator(T item)
            {
                this.Current = item;
                this.circularBuffer = null;
                this.metrics = null;
                this.targetCount = -1;
                this.metricIndex = 0;
            }

            internal Enumerator(CircularBuffer<T> circularBuffer, long targetCount)
            {
                this.Current = null;
                this.metrics = null;
                this.circularBuffer = circularBuffer;
                this.targetCount = targetCount;
                this.metricIndex = 0;
            }

            internal Enumerator(T[] metrics, long targetCount)
            {
                this.Current = null;
                this.circularBuffer = null;
                this.metrics = metrics;
                this.targetCount = targetCount;
                this.metricIndex = 0;
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
                if (typeof(T) == typeof(Metric))
                {
                    var metrics = this.metrics;

                    if (metrics != null)
                    {
                        if (this.metricIndex < this.targetCount)
                        {
                            this.Current = metrics[this.metricIndex];
                            this.metricIndex++;
                            return true;
                        }
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
