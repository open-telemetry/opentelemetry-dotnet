// <copyright file="MeasureMinMaxSumCountAggregator.cs" company="OpenTelemetry Authors">
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
    /// <typeparam name="T">Type of measure instrument.</typeparam>
    public class MeasureMinMaxSumCountAggregator<T> : Aggregator<T> 
        where T : struct
    {
        private Summary<T> summary;
        private Summary<T> checkPoint;
        private object updateLock = new object();

        public MeasureMinMaxSumCountAggregator()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }

            this.summary = new Summary<T>();
            this.checkPoint = new Summary<T>();
        }

        public override void Checkpoint()
        {
            this.checkPoint = Interlocked.Exchange(ref this.summary, new Summary<T>());
        }

        public override AggregationType GetAggregationType()
        {
            return AggregationType.Summary;
        }

        public override MetricData<T> ToMetricData()
        {
            var summaryData = new SummaryData<T>();
            summaryData.Count = this.checkPoint.Count;
            summaryData.Sum = this.checkPoint.Sum;
            summaryData.Min = this.checkPoint.Min;
            summaryData.Max = this.checkPoint.Max;
            summaryData.Timestamp = DateTime.UtcNow;
            return summaryData;
        }

        public override void Update(T value)
        {
            lock (this.updateLock)
            {
                this.summary.Count++;                
                if (typeof(T) == typeof(double))
                {
                    this.summary.Sum = (T)(object)((double)(object)this.summary.Sum + (double)(object)value);
                    this.summary.Max = (T)(object)Math.Max((double)(object)this.summary.Max, (double)(object)value);
                    this.summary.Min = (T)(object)Math.Min((double)(object)this.summary.Min, (double)(object)value);
                }
                else
                {
                    this.summary.Sum = (T)(object)((long)(object)this.summary.Sum + (long)(object)value);
                    this.summary.Max = (T)(object)Math.Max((long)(object)this.summary.Max, (long)(object)value);
                    this.summary.Min = (T)(object)Math.Min((long)(object)this.summary.Min, (long)(object)value);
                }
            }
        }

        private class Summary<Type> where Type : struct
        {
            public long Count;
            public Type Min;
            public Type Max;
            public Type Sum;

            public Summary()
            {
                this.Min = typeof(T) == typeof(double) ? (Type)(object)double.MaxValue : (Type)(object)long.MaxValue;
                this.Max = typeof(T) == typeof(double) ? (Type)(object)double.MinValue : (Type)(object)long.MinValue;
            }
        }
    }
}
