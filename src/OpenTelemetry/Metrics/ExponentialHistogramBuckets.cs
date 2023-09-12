// <copyright file="ExponentialHistogramBuckets.cs" company="OpenTelemetry Authors">
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

#nullable enable

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains the buckets of an exponential histogram.
/// </summary>
// Note: Does not implement IEnumerable<> to prevent accidental boxing.
public sealed class ExponentialHistogramBuckets
{
    private long[] buckets = Array.Empty<long>();
    private int size;

    internal ExponentialHistogramBuckets()
    {
    }

    /// <summary>
    /// Gets the exponential histogram offset.
    /// </summary>
    public int Offset { get; private set; }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="ExponentialHistogramBuckets"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this.buckets, this.size);

    internal void SnapshotBuckets(CircularBufferBuckets buckets)
    {
        if (this.buckets.Length != buckets.Capacity)
        {
            this.buckets = new long[buckets.Capacity];
        }

        this.size = buckets.Size;
        this.Offset = buckets.Offset;
        buckets.Copy(this.buckets);
    }

    internal ExponentialHistogramBuckets Copy()
    {
        var copy = new ExponentialHistogramBuckets
        {
            size = this.size,
            Offset = this.Offset,
            buckets = new long[this.buckets.Length],
        };
        Array.Copy(this.buckets, copy.buckets, this.buckets.Length);
        return copy;
    }

    /// <summary>
    /// Enumerates the bucket counts of an exponential histogram.
    /// </summary>
    // Note: Does not implement IEnumerator<> to prevent accidental boxing.
    public struct Enumerator
    {
        private readonly long[] buckets;
        private readonly int size;
        private int index;

        internal Enumerator(long[] buckets, int size)
        {
            this.index = 0;
            this.size = size;
            this.buckets = buckets;
            this.Current = default;
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
            if (this.index < this.size)
            {
                this.Current = this.buckets[this.index++];
                return true;
            }

            return false;
        }
    }
}
