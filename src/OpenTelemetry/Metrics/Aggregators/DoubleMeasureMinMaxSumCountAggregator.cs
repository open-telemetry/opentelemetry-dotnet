// <copyright file="DoubleMeasureMinMaxSumCountAggregator.cs" company="OpenTelemetry Authors">
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
    /// Aggregator which calculates summary (Min,Max,Sum,Count) from measures.
    /// </summary>
    public class DoubleMeasureMinMaxSumCountAggregator : Aggregator<double>
    {
        private DoubleSummary summary = new DoubleSummary();
        private DoubleSummary checkPoint = new DoubleSummary();
        private object updateLock = new object();

        public override void Checkpoint()
        {
            this.checkPoint = Interlocked.Exchange(ref this.summary, new DoubleSummary());
        }

        public override AggregationType GetAggregationType()
        {
            return AggregationType.Summary;
        }

        public override MetricData<double> ToMetricData()
        {
            return new SummaryData<double>
            {
                Count = this.checkPoint.Count,
                Sum = this.checkPoint.Sum,
                Min = this.checkPoint.Min,
                Max = this.checkPoint.Max,
                Timestamp = DateTime.UtcNow,
            };
        }

        public override void Update(double value)
        {
            lock (this.updateLock)
            {
                this.summary.Count++;
                this.summary.Sum += value;
                this.summary.Max = Math.Max(this.summary.Max, value);
                this.summary.Min = Math.Min(this.summary.Min, value);
            }
        }

        private class DoubleSummary
        {
            public long Count;
            public double Min;
            public double Max;
            public double Sum;

            public DoubleSummary()
            {
                this.Min = double.MaxValue;
                this.Max = double.MinValue;
            }
        }
    }
}
