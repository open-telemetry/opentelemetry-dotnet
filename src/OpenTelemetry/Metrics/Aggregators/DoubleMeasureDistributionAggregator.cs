// <copyright file="DoubleMeasureDistributionAggregator.cs" company="OpenTelemetry Authors">
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
    public class DoubleMeasureDistributionAggregator : Aggregator<double>
    {
        private readonly AggregationOptions aggregationOptions;
        private readonly double[] maxValue = { double.MinValue };
        private readonly double[] minValue = { double.MaxValue };
        private readonly Histogram<double> histogram;
        private DoubleDistributionData doubleDistributionData = new DoubleDistributionData();

        public DoubleMeasureDistributionAggregator(AggregationOptions aggregationOptions)
        {
            this.aggregationOptions = aggregationOptions;
            switch (aggregationOptions)
            {
                case DoubleExplicitDistributionOptions explicitOptions:
                    this.histogram = new DoubleExplicitHistogram(explicitOptions.Bounds);
                    break;
                case DoubleLinearDistributionOptions linearOptions:
                    this.histogram = new DoubleLinearHistogram(
                        linearOptions.Offset, linearOptions.Width, linearOptions.NumberOfFiniteBuckets);
                    break;
                case DoubleExponentialDistributionOptions exponentialOptions:
                    this.histogram = new DoubleExponentialHistogram(
                        exponentialOptions.Scale,
                        exponentialOptions.GrowthFactor,
                        exponentialOptions.NumberOfFiniteBuckets);
                    break;
                default:
                    throw new NotSupportedException(
                        "Unsupported aggregation options. Supported option types include: " +
                        "DoubleExplicitDistributionOptions, DoubleLinearDistributionOptions, " +
                        "DoubleExponentialDistributionOptions");
            }
        }

        /// <inheritdoc/>
        public override void Update(double value)
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
            lock (this.doubleDistributionData) lock (this.minValue) lock (this.maxValue)
            {
                var distributionData = this.histogram.GetDistributionAndClear();

                this.doubleDistributionData = new DoubleDistributionData
                {
                    BucketCounts = distributionData.BucketCounts,
                    Count = distributionData.Count,
                    Mean = distributionData.Mean,
                    SumOfSquaredDeviation = distributionData.SumOfSquaredDeviation,
                };
                if (this.doubleDistributionData.Count > 0)
                {
                    this.doubleDistributionData.Min = this.minValue[0];
                    this.doubleDistributionData.Max = this.maxValue[0];
                    this.minValue[0] = double.MaxValue;
                    this.maxValue[0] = double.MinValue;
                }
            }
        }

        /// <inheritdoc/>
        public override MetricData ToMetricData()
        {
            this.doubleDistributionData.AggregationOptions = this.aggregationOptions;
            this.doubleDistributionData.StartTimestamp = new DateTime(this.GetLastStartTimestamp().Ticks);
            this.doubleDistributionData.Timestamp = new DateTime(this.GetLastEndTimestamp().Ticks);

            return this.doubleDistributionData;
        }

        /// <inheritdoc/>
        public override AggregationType GetAggregationType()
        {
            return AggregationType.DoubleDistribution;
        }
    }
}
