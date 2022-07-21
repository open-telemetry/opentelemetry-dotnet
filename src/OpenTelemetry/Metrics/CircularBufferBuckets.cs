// <copyright file="CircularBufferBuckets.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// A histogram buckets implementation based on circular buffer.
    /// </summary>
    internal class CircularBufferBuckets
    {
        private long[] trait;
        private int begin = 0;
        private int end = -1;
        private int offset = 0;

        public CircularBufferBuckets(int capacity)
        {
            Guard.ThrowIfOutOfRange(capacity, min: 1);

            this.Capacity = capacity;
        }

        /// <summary>
        /// Gets the capacity of the <see cref="CircularBufferBuckets"/>.
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// Gets the size of the <see cref="CircularBufferBuckets"/>.
        /// </summary>
        public int Size => this.end - this.begin + 1;

        /// <summary>
        /// Attempts to increment the "count" of <c>Bucket[index]</c>.
        /// </summary>
        /// <param name="index">The index of the bucket.</param>
        /// <returns>
        /// Returns <c>true</c> if the increment attempt succeeded;
        /// <c>false</c> if the underlying buffer is running out of capacity.
        /// </returns>
        /// <remarks>
        /// The "index" value can be positive, zero or negative.
        /// </remarks>
        public bool TryIncrement(int index)
        {
            if (this.trait == null)
            {
                this.trait = new long[this.Capacity];

                this.begin = index;
                this.end = index;
                this.offset = index;
                this.trait[0 /* index - this.offset */] += 1;

                return true;
            }

            if (index > this.end)
            {
                if (index - this.begin + 1 > this.Capacity)
                {
                    return false;
                }

                this.end = index;
            }
            else if (index < this.begin)
            {
                if (this.end - index + 1 > this.Capacity)
                {
                    return false;
                }

                this.begin = index;
            }

            this.trait[index - this.offset] += 1; // TODO: rounding
            return true;
        }
    }
}
