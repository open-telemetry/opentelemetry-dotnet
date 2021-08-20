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
    internal class SumMetricAggregator : IAggregator
    {
        private readonly object lockUpdate = new object();

        private SumMetricLong sumMetricLong;
        private long sumLong = 0;
        private long lastSumLong = 0;

        private SumMetricDouble sumMetricDouble;
        private double sumDouble = 0;
        private double lastSumDouble = 0;

        private DateTimeOffset startTimeExclusive;
        private DateTimeOffset lastStartTimeExclusive;

        private bool isDeltaValue;
        private bool isMonotonicValue;

        internal SumMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, bool isDeltaValue, bool isMonotonicValue)
        {
            this.startTimeExclusive = startTimeExclusive;
            this.lastStartTimeExclusive = startTimeExclusive;
            this.sumMetricLong = new SumMetricLong(name, description, unit, meter, startTimeExclusive, attributes);
            this.sumMetricDouble = new SumMetricDouble(name, description, unit, meter, startTimeExclusive, attributes);
            this.isDeltaValue = isDeltaValue;
            this.isMonotonicValue = isMonotonicValue;
        }

        public void Update<T>(T value)
            where T : struct
        {
            long val = 0;
            double dval = 0;
            bool isDoubleValue = false;

            // TODO: Confirm the following casts doesn't cause boxing.
            // VL: It is expected the Compiler will optimize this heavily!
            if (typeof(T) == typeof(long))
            {
                val = (long)(object)value;
            }
            else if (typeof(T) == typeof(int))
            {
                val = (int)(object)value;
            }
            else if (typeof(T) == typeof(short))
            {
                val = (short)(object)value;
            }
            else if (typeof(T) == typeof(byte))
            {
                val = (byte)(object)value;
            }
            else if (typeof(T) == typeof(double))
            {
                dval = (double)(object)value;
                isDoubleValue = true;
            }
            else if (typeof(T) == typeof(float))
            {
                dval = (float)(object)value;
                isDoubleValue = true;
            }
            else
            {
                throw new Exception("Unsupported Type");
            }

            if (this.isMonotonicValue && (val < 0 || dval < 0))
            {
                // Ignore Non-Monotonic Values
                // TODO: Log?
            }
            else
            {
                if (this.isDeltaValue)
                {
                    // Updating with a Delta Value

                    if (isDoubleValue)
                    {
                        if (dval != 0)
                        {
                            lock (this.lockUpdate)
                            {
                                // TODO: Replace Lock with Interlocked.Add
                                // VL: We have multiple fields to lock/protect,
                                //     thus, an Interlocked.Add is not appropriate.

                                this.sumDouble += dval;
                            }
                        }
                    }
                    else
                    {
                        if (val != 0)
                        {
                            lock (this.lockUpdate)
                            {
                                this.sumLong += val;
                            }
                        }
                    }
                }
                else
                {
                    // Updating with a Cumulative Value

                    if (isDoubleValue)
                    {
                        lock (this.lockUpdate)
                        {
                            this.sumDouble = dval;
                        }
                    }
                    else
                    {
                        lock (this.lockUpdate)
                        {
                            this.sumLong = val;
                        }
                    }
                }
            }
        }

        public IMetric Collect(DateTimeOffset dt, bool collectDelta)
        {
            bool isDouble;

            if (collectDelta)
            {
                long sumLong;
                long lastSumLong;
                double sumDouble;
                double lastSumDouble;
                DateTimeOffset lastStartTimeExclusive;

                // Limit our locking to bare minimum
                lock (this.lockUpdate)
                {
                    sumLong = this.sumLong;
                    sumDouble = this.sumDouble;
                    lastSumLong = this.lastSumLong;
                    lastSumDouble = this.lastSumDouble;
                    lastStartTimeExclusive = this.lastStartTimeExclusive;

                    this.lastSumLong = sumLong;
                    this.lastSumDouble = sumDouble;
                    this.lastStartTimeExclusive = dt;
                }

                isDouble = sumDouble != 0;

                if (isDouble)
                {
                    // Include any long values also
                    sumDouble += sumLong;
                    lastSumDouble += lastSumLong;

                    this.sumMetricDouble.IsDeltaTemporality = true;
                    this.sumMetricDouble.StartTimeExclusive = lastStartTimeExclusive;
                    this.sumMetricDouble.EndTimeInclusive = dt;
                    this.sumMetricDouble.DoubleSum = sumDouble - lastSumDouble;
                }
                else
                {
                    this.sumMetricLong.IsDeltaTemporality = true;
                    this.sumMetricLong.StartTimeExclusive = lastStartTimeExclusive;
                    this.sumMetricLong.EndTimeInclusive = dt;
                    this.sumMetricLong.LongSum = sumLong - lastSumLong;
                }
            }
            else
            {
                long sumLong;
                double sumDouble;

                // Limit our locking to bare minimum
                lock (this.lockUpdate)
                {
                    sumLong = this.sumLong;
                    sumDouble = this.sumDouble;
                }

                isDouble = sumDouble != 0;

                if (isDouble)
                {
                    // Include any long values also
                    sumDouble += sumLong;

                    this.sumMetricDouble.IsDeltaTemporality = false;
                    this.sumMetricDouble.StartTimeExclusive = this.startTimeExclusive;
                    this.sumMetricDouble.EndTimeInclusive = dt;
                    this.sumMetricDouble.DoubleSum = sumDouble;
                }
                else
                {
                    this.sumMetricLong.IsDeltaTemporality = false;
                    this.sumMetricLong.StartTimeExclusive = this.startTimeExclusive;
                    this.sumMetricLong.EndTimeInclusive = dt;
                    this.sumMetricLong.LongSum = sumLong;
                }
            }

            // TODO: Confirm that this approach of
            // re-using the same instance is correct.
            // This avoids allocating a new instance.
            // It is read only for Exporters,
            // and also there is no parallel
            // Collect allowed.
            return isDouble ? (IMetric)this.sumMetricDouble : (IMetric)this.sumMetricLong;
        }

        public string ToDisplayString()
        {
            if (this.sumDouble != 0)
            {
                return $"Sum={this.sumDouble + this.sumLong}";
            }

            return $"Sum={this.sumLong}";
        }
    }
}
