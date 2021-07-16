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
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    internal class HistogramMetricAggregator : IHistogramMetric, IAggregator
    {
        private readonly object lockUpdate = new object();
        private List<HistogramBucket> buckets = new List<HistogramBucket>();

        internal HistogramMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes)
        {
            this.Name = name;
            this.Description = description;
            this.Unit = unit;
            this.Meter = meter;
            this.StartTimeExclusive = startTimeExclusive;
            this.Attributes = attributes;
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string Unit { get; private set; }

        public Meter Meter { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; private set; }

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public long PopulationCount { get; private set; }

        public double PopulationSum { get; private set; }

        public IEnumerable<HistogramBucket> Buckets => this.buckets;

        public void Update<T>(T value)
            where T : struct
        {
            // TODO: Implement Histogram!

            lock (this.lockUpdate)
            {
                this.PopulationCount++;
            }
        }

        public IMetric Collect(DateTimeOffset dt, bool isDelta)
        {
            if (this.PopulationCount == 0)
            {
                // TODO: Output stale markers
                return null;
            }

            var cloneItem = new HistogramMetricAggregator(this.Name, this.Description, this.Unit, this.Meter, this.StartTimeExclusive, this.Attributes);

            lock (this.lockUpdate)
            {
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.PopulationCount = this.PopulationCount;
                cloneItem.PopulationSum = this.PopulationSum;
                cloneItem.buckets = this.buckets;
                cloneItem.IsDeltaTemporality = isDelta;

                if (isDelta)
                {
                    this.StartTimeExclusive = dt;
                    this.PopulationCount = 0;
                    this.PopulationSum = 0;
                }
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Count={this.PopulationCount},Sum={this.PopulationSum}";
        }
    }
}
