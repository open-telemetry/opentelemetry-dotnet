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
    public class HistogramBuckets
    {
        internal readonly long[] CurrentBucketCounts;

        internal readonly long[] SnapshotBucketCounts;

        internal readonly double[] ExplicitBounds;

        internal readonly object LockObject;

        internal MetricPointPrimaryValueStorage Sum;

        internal HistogramBuckets(double[] histogramBounds)
        {
            this.ExplicitBounds = histogramBounds;
            this.CurrentBucketCounts = histogramBounds != null ? new long[histogramBounds.Length + 1] : null;
            this.SnapshotBucketCounts = histogramBounds != null ? new long[histogramBounds.Length + 1] : null;
            this.LockObject = new object();
        }

        public Enumerator GetEnumerator() => new(this);

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
                this.numberOfBuckets = histogramMeasurements.SnapshotBucketCounts == null ? 0 : histogramMeasurements.SnapshotBucketCounts.Length;
            }

            public HistogramBucket Current { get; private set; }

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
