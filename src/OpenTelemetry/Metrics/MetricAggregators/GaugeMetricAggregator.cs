// <copyright file="GaugeMetricAggregator.cs" company="OpenTelemetry Authors">
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
    internal class GaugeMetricAggregator : IGaugeMetric, IMetricBuilder
    {
        private readonly object lockUpdate = new object();
        private Type valueType;
        private long longValue;
        private double doubleValue;

        internal GaugeMetricAggregator(string name, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, bool isDelta)
        {
            this.Name = name;
            this.StartTimeExclusive = startTimeExclusive;
            this.EndTimeInclusive = startTimeExclusive;
            this.Attributes = attributes;
            this.IsDeltaTemporality = isDelta;
        }

        public string Name { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; }

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public IDataPoint LastValue
        {
            get
            {
                if (this.valueType == typeof(long))
                {
                    return DataPoint.CreateDataPoint(this.EndTimeInclusive, this.longValue, this.Attributes);
                }
                else if (this.valueType == typeof(double))
                {
                    return DataPoint.CreateDataPoint(this.EndTimeInclusive, this.doubleValue, this.Attributes);
                }
                else
                {
                    throw new Exception("Unsupported Type");
                }
            }
        }

        public void Update<T>(DateTimeOffset dt, T value)
            where T : struct
        {
            lock (this.lockUpdate)
            {
                this.EndTimeInclusive = dt;
                if (typeof(T) == typeof(int))
                {
                    // Promote to Long
                    this.valueType = typeof(long);
                    this.longValue = (int)(object)value;
                }
                else if (typeof(T) == typeof(long))
                {
                    this.valueType = typeof(T);
                    this.longValue = (long)(object)value;
                }
                else if (typeof(T) == typeof(double))
                {
                    this.valueType = typeof(T);
                    this.doubleValue = (double)(object)value;
                }
            }
        }

        public IMetric Collect(DateTimeOffset dt)
        {
            var cloneItem = new GaugeMetricAggregator(this.Name, this.StartTimeExclusive, this.Attributes, this.IsDeltaTemporality);

            lock (this.lockUpdate)
            {
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.valueType = this.valueType;
                cloneItem.longValue = this.longValue;
                cloneItem.doubleValue = this.doubleValue;
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Last={this.LastValue.Value}";
        }
    }
}
