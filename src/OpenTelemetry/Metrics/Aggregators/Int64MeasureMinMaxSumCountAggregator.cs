﻿// <copyright file="Int64MeasureMinMaxSumCountAggregator.cs" company="OpenTelemetry Authors">
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
    /// Aggregator which calculates summary (Min,Max,Sum,Count) from measures.
    /// </summary>
    public class Int64MeasureMinMaxSumCountAggregator : Aggregator<long>
    {
        private LongSummary summary = new LongSummary();
        private LongSummary checkPoint = new LongSummary();
        private object updateLock = new object();

        public override void Checkpoint()
        {
            this.checkPoint = Interlocked.Exchange(ref this.summary, new LongSummary());
        }

        public override AggregationType GetAggregationType()
        {
            return AggregationType.Int64Summary;
        }

        public override MetricData ToMetricData()
        {
            return new Int64SummaryData
            {
                Count = this.checkPoint.Count,
                Sum = this.checkPoint.Sum,
                Min = this.checkPoint.Min,
                Max = this.checkPoint.Max,
                Timestamp = DateTime.UtcNow,
            };
        }

        public override void Update(long value)
        {
            lock (this.updateLock)
            {
                this.summary.Count++;
                this.summary.Sum += value;
                this.summary.Max = Math.Max(this.summary.Max, value);
                this.summary.Min = Math.Min(this.summary.Min, value);
            }
        }

        private class LongSummary
        {
            public long Count;
            public long Min;
            public long Max;
            public long Sum;

            public LongSummary()
            {
                this.Min = long.MaxValue;
                this.Max = long.MinValue;
            }
        }
    }
}
