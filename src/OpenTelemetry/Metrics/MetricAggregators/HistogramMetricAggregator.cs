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
    internal class HistogramMetricAggregator : IAggregator
    {
        private static readonly double[] DefaultBoundaries = new double[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 1000 };

        private readonly object lockUpdate = new object();
        private HistogramBucket[] buckets;
        private long populationCount;
        private double populationSum;
        private double[] boundaries;
        private DateTimeOffset startTimeExclusive;
        private HistogramMetric histogramMetric;

        internal HistogramMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes)
            : this(name, description, unit, meter, startTimeExclusive, attributes, DefaultBoundaries)
        {
        }

        internal HistogramMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, double[] boundaries)
        {
            this.startTimeExclusive = startTimeExclusive;
            this.histogramMetric = new HistogramMetric(name, description, unit, meter, startTimeExclusive, attributes, boundaries.Length + 1);

            if (boundaries.Length == 0)
            {
                boundaries = DefaultBoundaries;
            }

            this.boundaries = boundaries;
            this.buckets = this.InitializeBucket(boundaries);
        }

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
                this.populationCount++;
                this.populationSum += val;
                this.buckets[i].Count++;
            }
        }

        public IMetric Collect(DateTimeOffset dt, bool isDelta)
        {
            if (this.populationCount == 0)
            {
                // TODO: Output stale markers
                return null;
            }

            lock (this.lockUpdate)
            {
                this.histogramMetric.StartTimeExclusive = this.startTimeExclusive;
                this.histogramMetric.EndTimeInclusive = dt;
                this.histogramMetric.PopulationCount = this.populationCount;
                this.histogramMetric.PopulationSum = this.populationSum;
                this.buckets.CopyTo(this.histogramMetric.BucketsArray, 0);
                this.histogramMetric.IsDeltaTemporality = isDelta;

                if (isDelta)
                {
                    this.startTimeExclusive = dt;
                    this.populationCount = 0;
                    this.populationSum = 0;
                    for (int i = 0; i < this.buckets.Length; i++)
                    {
                        this.buckets[i].Count = 0;
                    }
                }
            }

            // TODO: Confirm that this approach of
            // re-using the same instance is correct.
            // This avoids allocating a new instance.
            // It is read only for Exporters,
            // and also there is no parallel
            // Collect allowed.
            return this.histogramMetric;
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
