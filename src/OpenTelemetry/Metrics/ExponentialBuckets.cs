// <copyright file="ExponentialBuckets.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics;

internal class ExponentialBuckets
{
    private readonly CircularBufferBuckets buckets;

    internal ExponentialBuckets(CircularBufferBuckets buckets)
    {
        this.buckets = buckets;
    }

    public Enumerator GetEnumerator() => new(this.buckets);

    /// <summary>
    /// Enumerates the bucket counts of an exponential histogram.
    /// </summary>
    // Note: Does not implement IEnumerator<> to prevent accidental boxing.
    public struct Enumerator
    {
        private readonly int lastIndex;
        private readonly CircularBufferBuckets buckets;
        private int index;

        internal Enumerator(CircularBufferBuckets buckets)
        {
            this.buckets = buckets;
            this.index = buckets.Offset;
            this.Current = default;
            this.lastIndex = buckets.Size - 1;
        }

        /// <summary>
        /// Gets the bucket count at the current position of the enumerator.
        /// </summary>
        public long Current { get; private set; }

        /// <summary>
        /// Advances the enumerator to the next element of the <see
        /// cref="HistogramBuckets"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was
        /// successfully advanced to the next element; <see
        /// langword="false"/> if the enumerator has passed the end of the
        /// collection.</returns>
        public bool MoveNext()
        {
            if (this.index < this.buckets.Size)
            {
                this.Current = this.buckets[this.index++];
                return true;
            }

            return false;
        }
    }
}
