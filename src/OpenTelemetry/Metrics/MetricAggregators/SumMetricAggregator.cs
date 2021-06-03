// <copyright file="SumMetricAggregator.cs" company="OpenTelemetry Authors">
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
    internal class SumMetricAggregator : ISumMetric, IMetricBuilder
    {
        private readonly object lockUpdate = new object();
        private Type valueType;
        private long sum = 0;
        private double dsum = 0;
        private long count = 0;

        internal SumMetricAggregator(string name, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, bool isDelta, bool isMonotonic)
        {
            this.Name = name;
            this.StartTimeExclusive = startTimeExclusive;
            this.EndTimeInclusive = startTimeExclusive;
            this.Attributes = attributes;
            this.IsDeltaTemporality = isDelta;
            this.IsMonotonic = isMonotonic;
        }

        public string Name { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; }

        public bool IsMonotonic { get; }

        public IEnumerable<IDataPoint> Exemplars { get; private set; } = new List<IDataPoint>();

        public object Sum
        {
            get
            {
                if (this.valueType == typeof(int))
                {
                    return this.sum;
                }
                else if (this.valueType == typeof(double))
                {
                    return this.dsum;
                }

                return "Unknown";
            }
        }

        public void Update<T>(DateTimeOffset dt, T value)
            where T : struct
        {
            lock (this.lockUpdate)
            {
                this.EndTimeInclusive = dt;

                this.valueType = typeof(T);

                if (typeof(T) == typeof(int))
                {
                    var val = (int)(object)value;

                    if (this.IsMonotonic && val < 0)
                    {
                        return;
                    }

                    this.sum += val;
                    this.count++;
                }
                else if (typeof(T) == typeof(double))
                {
                    var val = (double)(object)value;

                    if (this.IsMonotonic && val < 0)
                    {
                        return;
                    }

                    this.dsum += val;
                    this.count++;
                }
                else
                {
                    throw new Exception("Unsupported Type");
                }
            }
        }

        public IMetric Collect(DateTimeOffset dt)
        {
            var cloneItem = new SumMetricAggregator(this.Name, this.StartTimeExclusive, this.Attributes, this.IsDeltaTemporality, this.IsMonotonic);

            lock (this.lockUpdate)
            {
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.valueType = this.valueType;
                cloneItem.count = this.count;
                cloneItem.sum = this.sum;
                cloneItem.dsum = this.dsum;

                if (this.IsDeltaTemporality)
                {
                    this.StartTimeExclusive = dt;
                    this.count = 0;
                    this.sum = 0;
                    this.dsum = 0;
                }
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Delta={this.IsDeltaTemporality},Count={this.count},Sum={this.Sum}";
        }
    }
}
