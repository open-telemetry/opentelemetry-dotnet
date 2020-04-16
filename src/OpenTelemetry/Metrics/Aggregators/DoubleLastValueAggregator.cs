// <copyright file="DoubleLastValueAggregator.cs" company="OpenTelemetry Authors">
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

using System;
using System.Threading;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Aggregators
{
    /// <summary>
    /// Simple aggregator that only keeps the last value.
    /// </summary>
    public class DoubleLastValueAggregator : Aggregator<double>
    {
        private double value;
        private double checkpoint;

        public override void Checkpoint()
        {
            Interlocked.Exchange(ref this.checkpoint, this.value);
        }

        public override MetricData<double> ToMetricData()
        {
            return new SumData<double>
            {
                Sum = this.checkpoint,
                Timestamp = DateTime.UtcNow,
            };
        }

        public override AggregationType GetAggregationType()
        {
            return AggregationType.DoubleSum;
        }

        public override void Update(double newValue)
        {
            Interlocked.Exchange(ref this.value, newValue);
        }
    }
}
