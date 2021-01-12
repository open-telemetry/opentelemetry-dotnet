// <copyright file="Int64MeasureDistributionAggregator.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Histogram;

namespace OpenTelemetry.Metrics.Aggregators
{
    public class Int64MeasureDistributionAggregator : Aggregator<long>
    {
        private readonly AggregationOptions aggregationOptions;
        private readonly Histogram<long> histogram;
        private readonly long[] minValue = { long.MaxValue };
        private readonly long[] maxValue = { long.MinValue };
        private Int64DistributionData int64DistributionData = new Int64DistributionData();

        public Int64MeasureDistributionAggregator(AggregationOptions aggregationOptions)
        {
            this.aggregationOptions = aggregationOptions;
            switch (aggregationOptions)
            {
                case Int64ExplicitDistributionOptions explicitOptions:
                    this.histogram = new Int64ExplicitHistogram(explicitOptions.Bounds);
                    break;
                case Int64LinearDistributionOptions linearOptions:
                    this.histogram = new Int64LinearHistogram(
                        linearOptions.Offset, linearOptions.Width, linearOptions.NumberOfFiniteBuckets);
                    break;
                case Int64ExponentialDistributionOptions exponentialOptions:
                    this.histogram = new Int64ExponentialHistogram(
                        exponentialOptions.Scale,
                        exponentialOptions.GrowthFactor,
                        exponentialOptions.NumberOfFiniteBuckets);
                    break;
                default:
                    throw new NotSupportedException(
                        "Unsupported aggregation options. Supported option types include: " +
                        "Int64ExplicitDistributionOptions, Int64LinearDistributionOptions, " +
                        "Int64ExponentialDistributionOptions");
            }
        }

        /// <inheritdoc/>
        public override void Update(long value)
        {
            if (value < this.minValue[0])
            {
                Interlocked.Exchange(ref this.minValue[0], Math.Min(value, this.minValue[0]));
            }

            if (value > this.maxValue[0])
            {
                Interlocked.Exchange(ref this.maxValue[0], Math.Max(value, this.maxValue[0]));
            }

            this.histogram.RecordValue(value);
        }

        /// <inheritdoc/>
        public override void Checkpoint()
        {
            base.Checkpoint();
            lock (this.int64DistributionData) lock (this.minValue) lock (this.maxValue)
            {
                var distributionData = this.histogram.GetDistributionAndClear();
                this.int64DistributionData = new Int64DistributionData
                {
                    BucketCounts = distributionData.BucketCounts,
                    Count = distributionData.Count,
                    Mean = distributionData.Mean,
                    SumOfSquaredDeviation = distributionData.SumOfSquaredDeviation,
                };
                if (this.int64DistributionData.Count > 0)
                {
                    this.int64DistributionData.Min = this.minValue[0];
                    this.int64DistributionData.Max = this.maxValue[0];
                    this.minValue[0] = long.MaxValue;
                    this.maxValue[0] = long.MinValue;
                }
            }
        }

        /// <inheritdoc/>
        public override MetricData ToMetricData()
        {
            this.int64DistributionData.AggregationOptions = this.aggregationOptions;
            this.int64DistributionData.StartTimestamp = new DateTime(this.GetLastStartTimestamp().Ticks);
            this.int64DistributionData.Timestamp = new DateTime(this.GetLastEndTimestamp().Ticks);

            return this.int64DistributionData;
        }

        /// <inheritdoc/>
        public override AggregationType GetAggregationType()
        {
            return AggregationType.Int64Distribution;
        }
    }
}
