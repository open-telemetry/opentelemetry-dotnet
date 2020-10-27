// <copyright file="Int64BoundMeasureMetricSdk.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    internal class Int64BoundMeasureMetricSdk : BoundMeasureMetricSdkBase<long>
    {
        private readonly Aggregator<long> measureAggregator;

        internal Int64BoundMeasureMetricSdk(AggregationType aggregationType)
        {
            switch (aggregationType)
            {
                case AggregationType.Int64Summary:
                    this.measureAggregator = new Int64MeasureMinMaxSumCountAggregator();
                    break;
                case AggregationType.Int64Distribution:
                    this.measureAggregator = new Int64MeasureDistributionAggregator();
                    break;
                default:
                    throw new NotSupportedException("Unrecognized AggregationType");
            }
        }

        public override void Record(in SpanContext context, long value)
        {
            this.measureAggregator.Update(value);
        }

        public override void Record(in Baggage context, long value)
        {
            this.measureAggregator.Update(value);
        }

        public override Aggregator<long> GetAggregator()
        {
            return this.measureAggregator;
        }
    }
}
