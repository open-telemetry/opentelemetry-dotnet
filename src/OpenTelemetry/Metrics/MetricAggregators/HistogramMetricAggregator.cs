// <copyright file="HistogramMetricAggregator.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

namespace OpenTelemetry.Metrics
{
    public class HistogramMetricAggregator : IHistogramMetric, IAggregator
    {
        private readonly object lockUpdate = new object();
        private List<HistogramBucket> buckets = new List<HistogramBucket>();

        public HistogramMetricAggregator(bool isDelta)
        {
            this.IsDeltaTemporality = isDelta;
        }

        public string Name { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; }

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public long PopulationCount { get; private set; }

        public double PopulationSum { get; private set; }

        public IEnumerable<HistogramBucket> Buckets => this.buckets;

        public void Init(string name, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes)
        {
            this.Name = name;
            this.StartTimeExclusive = startTimeExclusive;
            this.EndTimeInclusive = startTimeExclusive;
            this.Attributes = attributes;
        }

        public void Update<T>(DateTimeOffset dt, T value)
            where T : struct
        {
            // TODO: Implement Histogram!

            lock (this.lockUpdate)
            {
                this.EndTimeInclusive = dt;
                this.PopulationCount++;
            }
        }

        public IMetric Collect(DateTimeOffset dt)
        {
            if (this.PopulationCount == 0)
            {
                // TODO: Output stale markers
                return null;
            }

            var cloneItem = new HistogramMetricAggregator(this.IsDeltaTemporality);

            lock (this.lockUpdate)
            {
                cloneItem.Init(this.Name, this.StartTimeExclusive, this.Attributes);
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.PopulationCount = this.PopulationCount;
                cloneItem.PopulationSum = this.PopulationSum;
                cloneItem.buckets = this.buckets;

                this.StartTimeExclusive = dt;
                this.PopulationCount = 0;
                this.PopulationSum = 0;
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Delta={this.IsDeltaTemporality},Count={this.PopulationCount},Sum={this.PopulationSum}";
        }
    }
}
