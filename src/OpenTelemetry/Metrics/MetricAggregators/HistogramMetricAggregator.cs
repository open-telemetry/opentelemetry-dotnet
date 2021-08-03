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
        private static readonly double[] DefaultBoundaries = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000 };

        private readonly object lockUpdate = new object();
        private HistogramBucket[] buckets;

        private double[] boundaries;

        internal HistogramMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes)
            : this(name, description, unit, meter, startTimeExclusive, attributes, DefaultBoundaries)
        {
        }

        internal HistogramMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, double[] boundaries)
        {
            this.Name = name;
            this.Description = description;
            this.Unit = unit;
            this.Meter = meter;
            this.StartTimeExclusive = startTimeExclusive;
            this.Attributes = attributes;

            if (boundaries.Length == 0)
            {
                boundaries = DefaultBoundaries;
            }

            this.boundaries = boundaries;
            this.buckets = this.InitializeBucket(boundaries);
            this.MetricType = MetricType.Summary;
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

        public MetricType MetricType { get; private set; }

        public IEnumerable<HistogramBucket> Buckets => this.buckets;

        public void Update<T>(T value)
            where T : struct
        {
            // promote value to be a double

            double val;
            if (typeof(T) == typeof(long))
            {
                val = (long)(object)value;
            }
            else if (typeof(T) == typeof(double))
            {
                val = (double)(object)value;
            }
            else
            {
                throw new Exception("Unsupported Type!");
            }

            // Determine the bucket index

            int i;
            for (i = 0; i < this.boundaries.Length; i++)
            {
                if (val < this.boundaries[i])
                {
                    break;
                }
            }

            lock (this.lockUpdate)
            {
                this.PopulationCount++;
                this.PopulationSum += val;
                this.buckets[i].Count++;
            }
        }

        public IMetric Collect(DateTimeOffset dt, bool isDelta)
        {
            if (this.PopulationCount == 0)
            {
                // TODO: Output stale markers
                return null;
            }

            var cloneItem = new HistogramMetricAggregator(this.Name, this.Description, this.Unit, this.Meter, this.StartTimeExclusive, this.Attributes, this.boundaries);

            lock (this.lockUpdate)
            {
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.PopulationCount = this.PopulationCount;
                cloneItem.PopulationSum = this.PopulationSum;
                cloneItem.boundaries = this.boundaries;
                this.buckets.CopyTo(cloneItem.buckets, 0);
                cloneItem.IsDeltaTemporality = isDelta;

                if (isDelta)
                {
                    this.StartTimeExclusive = dt;
                    this.PopulationCount = 0;
                    this.PopulationSum = 0;
                    for (int i = 0; i < this.buckets.Length; i++)
                    {
                        this.buckets[i].Count = 0;
                    }
                }
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Count={this.PopulationCount},Sum={this.PopulationSum}";
        }

        private HistogramBucket[] InitializeBucket(double[] boundaries)
        {
            var buckets = new HistogramBucket[boundaries.Length + 1];

            var lastBoundary = boundaries[0];
            for (int i = 0; i < buckets.Length; i++)
            {
                if (i == 0)
                {
                    // LowBoundary is inclusive
                    buckets[i].LowBoundary = double.NegativeInfinity;

                    // HighBoundary is exclusive
                    buckets[i].HighBoundary = boundaries[i];
                }
                else if (i < boundaries.Length)
                {
                    // LowBoundary is inclusive
                    buckets[i].LowBoundary = lastBoundary;

                    // HighBoundary is exclusive
                    buckets[i].HighBoundary = boundaries[i];
                }
                else
                {
                    // LowBoundary and HighBoundary are inclusive
                    buckets[i].LowBoundary = lastBoundary;
                    buckets[i].HighBoundary = double.PositiveInfinity;
                }

                buckets[i].Count = 0;

                if (i < boundaries.Length)
                {
                    if (boundaries[i] < lastBoundary)
                    {
                        throw new ArgumentException("Boundary values must be increasing.", nameof(boundaries));
                    }

                    lastBoundary = boundaries[i];
                }
            }

            return buckets;
        }
    }
}
