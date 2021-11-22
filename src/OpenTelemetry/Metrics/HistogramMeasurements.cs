// <copyright file="HistogramMeasurements.cs" company="OpenTelemetry Authors">
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
    internal class HistogramMeasurements
    {
        internal readonly long[] BucketCounts;

        internal readonly long[] AggregatedBucketCounts;

        internal readonly double[] ExplicitBounds;

        internal readonly object LockObject;

        internal long CountVal;

        internal long Count;

        internal double SumVal;

        internal double Sum;

        internal HistogramMeasurements(double[] histogramBounds)
        {
            this.ExplicitBounds = histogramBounds;
            this.BucketCounts = histogramBounds != null ? new long[histogramBounds.Length + 1] : null;
            this.AggregatedBucketCounts = histogramBounds != null ? new long[histogramBounds.Length + 1] : null;
            this.LockObject = new object();
        }
    }
}
