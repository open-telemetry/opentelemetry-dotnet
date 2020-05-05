// <copyright file="DoubleBoundMeasureMetricSdk.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using OpenTelemetry.Context;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Metrics
{
    internal class DoubleBoundMeasureMetricSdk : BoundMeasureMetricSdkBase<double>
    {
        private readonly DoubleMeasureMinMaxSumCountAggregator measureAggregator = new DoubleMeasureMinMaxSumCountAggregator();

        public override void Record(in SpanContext context, double value)
        {
            this.measureAggregator.Update(value);
        }

        public override void Record(in DistributedContext context, double value)
        {
            this.measureAggregator.Update(value);
        }

        public override Aggregator<double> GetAggregator()
        {
            return this.measureAggregator;
        }
    }
}
