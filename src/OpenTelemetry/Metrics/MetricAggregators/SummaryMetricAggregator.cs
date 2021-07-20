// <copyright file="SummaryMetricAggregator.cs" company="OpenTelemetry Authors">
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
    internal class SummaryMetricAggregator : ISummaryMetric, IAggregator
    {
        private readonly object lockUpdate = new object();

        private List<ValueAtQuantile> quantiles = new List<ValueAtQuantile>();

        internal SummaryMetricAggregator()
        {
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string Unit { get; private set; }

        public Meter Meter { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public long PopulationCount { get; private set; }

        public double PopulationSum { get; private set; }

        public IEnumerable<ValueAtQuantile> Quantiles => this.quantiles;

        public void Init(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes)
        {
            this.Name = name;
            this.Description = description;
            this.Unit = unit;
            this.Meter = meter;
            this.StartTimeExclusive = startTimeExclusive;
            this.EndTimeInclusive = startTimeExclusive;
            this.Attributes = attributes;
        }

        public void Update<T>(T value)
            where T : struct
        {
            // TODO: Implement Summary!

            lock (this.lockUpdate)
            {
                if (typeof(T) == typeof(long))
                {
                    var val = (long)(object)value;
                    this.PopulationSum += (double)val;
                    this.PopulationCount++;
                }
                else if (typeof(T) == typeof(double))
                {
                    var val = (double)(object)value;
                    this.PopulationSum += (double)val;
                    this.PopulationCount++;
                }
            }
        }

        public IMetric Collect(DateTimeOffset dt)
        {
            if (this.PopulationCount == 0)
            {
                // TODO: Output stale markers
                return null;
            }

            var cloneItem = new SummaryMetricAggregator();

            lock (this.lockUpdate)
            {
                cloneItem.Init(this.Name, this.Description, this.Unit, this.Meter, this.StartTimeExclusive, this.Attributes);
                cloneItem.EndTimeInclusive = dt;
                cloneItem.PopulationCount = this.PopulationCount;
                cloneItem.PopulationSum = this.PopulationSum;
                cloneItem.quantiles = this.quantiles;

                this.StartTimeExclusive = dt;
                this.PopulationCount = 0;
                this.PopulationSum = 0;
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Count={this.PopulationCount},Sum={this.PopulationSum}";
        }
    }
}
