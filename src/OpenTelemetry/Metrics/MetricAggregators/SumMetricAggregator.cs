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
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    internal class SumMetricAggregator : ISumMetric, IAggregator
    {
        private readonly object lockUpdate = new object();
        private Type valueType;
        private long sumLong = 0;
        private double sumDouble = 0;

        internal SumMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes)
        {
            this.Name = name;
            this.Description = description;
            this.Unit = unit;
            this.Meter = meter;
            this.StartTimeExclusive = startTimeExclusive;
            this.Attributes = attributes;
            this.IsMonotonic = true;
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string Unit { get; private set; }

        public Meter Meter { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; private set; }

        public bool IsMonotonic { get; }

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public IDataValue Sum
        {
            get
            {
                if (this.valueType == typeof(long))
                {
                    return new DataValue(this.sumLong);
                }
                else if (this.valueType == typeof(double))
                {
                    return new DataValue(this.sumDouble);
                }

                throw new Exception("Unsupported Type");
            }
        }

        public void Update<T>(T value)
            where T : struct
        {
            lock (this.lockUpdate)
            {
                if (typeof(T) == typeof(long))
                {
                    this.valueType = typeof(T);
                    var val = (long)(object)value;
                    if (val < 0)
                    {
                        // TODO: log?
                        // Also, this validation can be done in earlier stage.
                    }
                    else
                    {
                        this.sumLong += val;
                    }
                }
                else if (typeof(T) == typeof(double))
                {
                    this.valueType = typeof(T);
                    var val = (double)(object)value;
                    if (val < 0)
                    {
                        // TODO: log?
                        // Also, this validation can be done in earlier stage.
                    }
                    else
                    {
                        this.sumDouble += val;
                    }
                }
                else
                {
                    throw new Exception("Unsupported Type");
                }
            }
        }

        public IMetric Collect(DateTimeOffset dt, bool isDelta)
        {
            var cloneItem = new SumMetricAggregator(this.Name, this.Description, this.Unit, this.Meter, this.StartTimeExclusive, this.Attributes);

            lock (this.lockUpdate)
            {
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.valueType = this.valueType;
                cloneItem.sumLong = this.sumLong;
                cloneItem.sumDouble = this.sumDouble;
                cloneItem.IsDeltaTemporality = isDelta;

                if (isDelta)
                {
                    this.StartTimeExclusive = dt;
                    this.sumLong = 0;
                    this.sumDouble = 0;
                }
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Sum={this.Sum.Value}";
        }
    }
}
