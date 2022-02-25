// <copyright file="HistogramMetric.cs" company="OpenTelemetry Authors">
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
    internal class HistogramMetric : IHistogramMetric
    {
        internal HistogramMetric(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, int bucketCount)
        {
            this.Name = name;
            this.Description = description;
            this.Unit = unit;
            this.Meter = meter;
            this.StartTimeExclusive = startTimeExclusive;
            this.Attributes = attributes;
            this.MetricType = MetricType.Histogram;
            this.BucketsArray = new HistogramBucket[bucketCount];
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string Unit { get; private set; }

        public Meter Meter { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; internal set; }

        public DateTimeOffset EndTimeInclusive { get; internal set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; internal set; }

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public long PopulationCount { get; internal set; }

        public double PopulationSum { get; internal set; }

        public IEnumerable<HistogramBucket> Buckets => this.BucketsArray;

        public MetricType MetricType { get; private set; }

        internal HistogramBucket[] BucketsArray { get; set; }

        public string ToDisplayString()
        {
            return $"Count={this.PopulationCount},Sum={this.PopulationSum}";
        }
    }
}
