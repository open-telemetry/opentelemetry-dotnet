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
    internal class SumMetricAggregator : ISumMetric, IAggregator
    {
        private readonly object lockUpdate = new object();
        private Type valueType;
        private long sumLong = 0;
        private double sumDouble = 0;

        internal SumMetricAggregator(string name, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, bool isDelta)
        {
            this.Name = name;
            this.StartTimeExclusive = startTimeExclusive;
            this.EndTimeInclusive = startTimeExclusive;
            this.Attributes = attributes;
            this.IsDeltaTemporality = isDelta;
            this.IsMonotonic = true;
        }

        public string Name { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; }

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

        public void Update<T>(DateTimeOffset dt, T value)
            where T : struct
        {
            lock (this.lockUpdate)
            {
                this.EndTimeInclusive = dt;

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

        public IMetric Collect(DateTimeOffset dt)
        {
            var cloneItem = new SumMetricAggregator(this.Name, this.StartTimeExclusive, this.Attributes, this.IsDeltaTemporality);

            lock (this.lockUpdate)
            {
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.valueType = this.valueType;
                cloneItem.sumLong = this.sumLong;
                cloneItem.sumDouble = this.sumDouble;

                if (this.IsDeltaTemporality)
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
            return $"Delta={this.IsDeltaTemporality},Monotonic={this.IsMonotonic},Sum={this.Sum.Value}";
        }
    }
}
