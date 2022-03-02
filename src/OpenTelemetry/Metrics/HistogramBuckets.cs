// <copyright file="HistogramBuckets.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// A collection of <see cref="HistogramBucket"/>s associated with a histogram metric type.
    /// </summary>
    // Note: Does not implement IEnumerable<> to prevent accidental boxing.
    public class HistogramBuckets
    {
        internal readonly double[] ExplicitBounds;

        internal readonly long[] RunningBucketCounts;

        internal readonly long[] SnapshotBucketCounts;

        internal double RunningSum;

        internal double SnapshotSum;

        internal int IsCriticalSectionOccupied = 0;

        internal HistogramBuckets(double[] explicitBounds)
        {
            this.ExplicitBounds = explicitBounds;
            this.RunningBucketCounts = explicitBounds != null ? new long[explicitBounds.Length + 1] : null;
            this.SnapshotBucketCounts = explicitBounds != null ? new long[explicitBounds.Length + 1] : new long[0];
        }

        internal object LockObject => this.SnapshotBucketCounts;

        public Enumerator GetEnumerator() => new(this);

        /// <summary>
        /// Enumerates the elements of a <see cref="HistogramBuckets"/>.
        /// </summary>
        // Note: Does not implement IEnumerator<> to prevent accidental boxing.
        public struct Enumerator
        {
            private readonly int numberOfBuckets;
            private readonly HistogramBuckets histogramMeasurements;
            private int index;

            internal Enumerator(HistogramBuckets histogramMeasurements)
            {
                this.histogramMeasurements = histogramMeasurements;
                this.index = 0;
                this.Current = default;
                this.numberOfBuckets = histogramMeasurements.SnapshotBucketCounts.Length;
            }

            /// <summary>
            /// Gets the <see cref="HistogramBucket"/> at the current position of the enumerator.
            /// </summary>
            public HistogramBucket Current { get; private set; }

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
                if (this.index < this.numberOfBuckets)
                {
                    double explicitBound = this.index < this.numberOfBuckets - 1
                        ? this.histogramMeasurements.ExplicitBounds[this.index]
                        : double.PositiveInfinity;
                    long bucketCount = this.histogramMeasurements.SnapshotBucketCounts[this.index];
                    this.Current = new HistogramBucket(explicitBound, bucketCount);
                    this.index++;
                    return true;
                }

                return false;
            }
        }
    }
}
