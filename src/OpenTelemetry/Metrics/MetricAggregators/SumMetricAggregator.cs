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
        private long sumPos = 0;
        private double dsumPos = 0;
        private long sumNeg = 0;
        private double dsumNeg = 0;

        internal SumMetricAggregator(bool isDelta, bool isMonotonic)
        {
            this.IsDeltaTemporality = isDelta;
            this.IsMonotonic = isMonotonic;
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string Unit { get; private set; }

        public Meter Meter { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; private set; }

        public bool IsMonotonic { get; private set; }

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public IDataValue Sum
        {
            get
            {
                if (this.valueType == typeof(long))
                {
                    long sum;

                    if (this.IsMonotonic)
                    {
                        sum = this.sumPos + (long)this.dsumPos;
                    }
                    else
                    {
                        sum = this.sumPos + (long)this.dsumPos + this.sumNeg + (long)this.dsumNeg;
                    }

                    return new DataValue(sum);
                }
                else if (this.valueType == typeof(double))
                {
                    double sum;

                    if (this.IsMonotonic)
                    {
                        sum = (double)this.sumPos + this.dsumPos;
                    }
                    else
                    {
                        sum = (double)this.sumPos + this.dsumPos + (double)this.sumNeg + this.dsumNeg;
                    }

                    return new DataValue(sum);
                }

                throw new Exception("Unsupported Type");
            }
        }

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
            lock (this.lockUpdate)
            {
                if (typeof(T) == typeof(long))
                {
                    this.valueType = typeof(T);
                    var val = (long)(object)value;
                    if (val < 0)
                    {
                        this.sumNeg += val;
                    }
                    else
                    {
                        this.sumPos += val;
                    }
                }
                else if (typeof(T) == typeof(double))
                {
                    this.valueType = typeof(T);
                    var val = (double)(object)value;
                    if (val < 0)
                    {
                        this.dsumNeg += val;
                    }
                    else
                    {
                        this.dsumPos += val;
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
            var cloneItem = new SumMetricAggregator(this.IsDeltaTemporality, this.IsMonotonic);

            lock (this.lockUpdate)
            {
                cloneItem.Init(this.Name, this.Description, this.Unit, this.Meter, this.StartTimeExclusive, this.Attributes);
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.valueType = this.valueType;
                cloneItem.sumPos = this.sumPos;
                cloneItem.sumNeg = this.sumNeg;
                cloneItem.dsumPos = this.dsumPos;
                cloneItem.dsumNeg = this.dsumNeg;

                if (this.IsDeltaTemporality)
                {
                    this.StartTimeExclusive = dt;
                    this.sumPos = 0;
                    this.sumNeg = 0;
                    this.dsumPos = 0;
                    this.dsumNeg = 0;
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
