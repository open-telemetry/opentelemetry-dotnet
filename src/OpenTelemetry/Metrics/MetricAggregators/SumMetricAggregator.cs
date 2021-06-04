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
        private long sumPos = 0;
        private double dsumPos = 0;
        private long countPos = 0;
        private long sumNeg = 0;
        private double dsumNeg = 0;
        private long countNeg = 0;

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

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public object Sum
        {
            get
            {
                if (this.valueType == typeof(long))
                {
                    if (this.IsMonotonic)
                    {
                        return this.sumPos + (long)this.dsumPos;
                    }
                    else
                    {
                        return this.sumPos + (long)this.dsumPos + this.sumNeg + (long)this.dsumNeg;
                    }
                }
                else if (this.valueType == typeof(double))
                {
                    if (this.IsMonotonic)
                    {
                        return this.dsumPos + (double)this.sumPos;
                    }
                    else
                    {
                        return this.dsumPos + (double)this.sumPos + this.dsumNeg + (double)this.sumNeg;
                    }
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

                if (typeof(T) == typeof(int))
                {
                    // Promote to Long
                    this.valueType = typeof(long);
                    var val = (long)(int)(object)value;

                    if (val >= 0)
                    {
                        this.sumPos += val;
                        this.countPos++;
                    }
                    else
                    {
                        this.sumNeg += val;
                        this.countNeg++;
                    }
                }
                else if (typeof(T) == typeof(long))
                {
                    this.valueType = typeof(T);
                    var val = (long)(object)value;

                    if (val >= 0)
                    {
                        this.sumPos += val;
                        this.countPos++;
                    }
                    else
                    {
                        this.sumNeg += val;
                        this.countNeg++;
                    }
                }
                else if (typeof(T) == typeof(double))
                {
                    this.valueType = typeof(T);
                    var val = (double)(object)value;

                    if (val >= 0)
                    {
                        this.dsumPos += val;
                        this.countPos++;
                    }
                    else
                    {
                        this.dsumNeg += val;
                        this.countNeg++;
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
            var cloneItem = new SumMetricAggregator(this.Name, this.StartTimeExclusive, this.Attributes, this.IsDeltaTemporality, this.IsMonotonic);

            lock (this.lockUpdate)
            {
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.valueType = this.valueType;
                cloneItem.countPos = this.countPos;
                cloneItem.sumPos = this.sumPos;
                cloneItem.dsumPos = this.dsumPos;
                cloneItem.countNeg = this.countNeg;
                cloneItem.sumNeg = this.sumNeg;
                cloneItem.dsumNeg = this.dsumNeg;

                if (this.IsDeltaTemporality)
                {
                    this.StartTimeExclusive = dt;
                    this.countPos = 0;
                    this.sumPos = 0;
                    this.dsumPos = 0;
                    this.countNeg = 0;
                    this.sumNeg = 0;
                    this.dsumNeg = 0;
                }
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Delta={this.IsDeltaTemporality},Count={this.countPos},Sum={this.Sum}";
        }
    }
}
