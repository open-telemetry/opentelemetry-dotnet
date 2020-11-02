// <copyright file="Int64LinearHistogram.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Histogram
{
    public class Int64LinearHistogram : LinearHistogram<long>
    {
        public Int64LinearHistogram(long offset, long width, int numberOfFiniteBuckets)
            : base(offset, width, numberOfFiniteBuckets)
        {
        }

        protected override int GetBucketIndex(long valueToAdd)
        {
            var doubleIndex = (double)(valueToAdd - this.Offset) / this.Width;

            return (int)Math.Floor(doubleIndex);
        }

        protected override DistributionData GetDistributionData()
        {
            var mean = this.Values.Average();

            return new DistributionData()
            {
                BucketCounts = this.GetBucketCounts(),
                Count = this.Values.Count,
                Mean = this.Values.Average(),
                SumOfSquaredDeviation = HistogramHelper.GetSumOfSquaredDeviation(
                    mean, this.Values.Select(val => (double)val).ToArray()),
            };
        }

        protected override long GetHighestBound()
        {
            return this.Offset + (this.Width * this.NumberOfFiniteBuckets);
        }
    }
}
