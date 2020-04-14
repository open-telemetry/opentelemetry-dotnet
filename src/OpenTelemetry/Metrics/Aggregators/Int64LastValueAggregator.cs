// <copyright file="Int64LastValueAggregator.cs" company="OpenTelemetry Authors">
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
    public class Int64LastValueAggregator : Aggregator<long>
    {
        private long value;
        private long checkpoint;

        public override void Checkpoint()
        {
            Interlocked.Exchange(ref this.checkpoint, this.value);
        }

        public override MetricData<long> ToMetricData()
        {
            return new SumData<long>
            {
                Sum = this.checkpoint,
                Timestamp = DateTime.UtcNow,
            };
        }

        public override AggregationType GetAggregationType()
        {
            return AggregationType.LongSum;
        }

        public override void Update(long newValue)
        {
            Interlocked.Exchange(ref this.value, newValue);
        }
    }
}
