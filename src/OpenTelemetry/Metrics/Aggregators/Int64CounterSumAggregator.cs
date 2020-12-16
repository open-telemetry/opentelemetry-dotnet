// <copyright file="Int64CounterSumAggregator.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Aggregators
{
    /// <summary>
    /// Basic aggregator which calculates a Sum from individual measurements.
    /// </summary>
    [Obsolete("Metrics API/SDK is not recommended for production. See https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501 for more information on metrics support.")]
    public class Int64CounterSumAggregator : Aggregator<long>
    {
        private long sum;
        private long checkPoint;

        /// <inheritdoc/>
        public override void Checkpoint()
        {
            // checkpoints the current running sum into checkpoint, and starts counting again.
            base.Checkpoint();
            this.checkPoint = Interlocked.Exchange(ref this.sum, 0);
        }

        /// <inheritdoc/>
        public override MetricData ToMetricData()
        {
            return new Int64SumData
            {
                StartTimestamp = new DateTime(this.GetLastStartTimestamp().Ticks),
                Sum = this.checkPoint,
                Timestamp = new DateTime(this.GetLastEndTimestamp().Ticks),
            };
        }

        /// <inheritdoc/>
        public override AggregationType GetAggregationType()
        {
            return AggregationType.LongSum;
        }

        /// <inheritdoc/>
        public override void Update(long value)
        {
            // Adds value to the running total in a thread safe manner.
            Interlocked.Add(ref this.sum, value);
        }
    }
}
