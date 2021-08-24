// <copyright file="SumMetricAggregatorLong.cs" company="OpenTelemetry Authors">
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
    internal class SumMetricAggregatorLong : IAggregator
    {
        private readonly object lockUpdate = new object();
        private long sumLong = 0;
        private long lastSumLong = 0;
        private bool treatIncomingMeasurementAsDelta;
        private SumMetricLong sumMetricLong;
        private DateTimeOffset startTimeExclusive;

        internal SumMetricAggregatorLong(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, bool treatIncomingMeasurementAsDelta)
        {
            this.startTimeExclusive = startTimeExclusive;
            this.treatIncomingMeasurementAsDelta = treatIncomingMeasurementAsDelta;
            this.sumMetricLong = new SumMetricLong(name, description, unit, meter, startTimeExclusive, attributes);
        }

        public void Update<T>(T value)
            where T : struct
        {
            // TODO: Replace Lock with Interlocked.Add
            lock (this.lockUpdate)
            {
                if (typeof(T) == typeof(long))
                {
                    // TODO: Confirm this doesn't cause boxing.
                    var val = (long)(object)value;
                    if (val < 0)
                    {
                        // TODO: log?
                        // Also, this validation can be done in earlier stage.
                    }
                    else
                    {
                        if (this.treatIncomingMeasurementAsDelta)
                        {
                            this.sumLong += val;
                        }
                        else
                        {
                            this.sumLong = val;
                        }
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
            lock (this.lockUpdate)
            {
                this.sumMetricLong.StartTimeExclusive = this.startTimeExclusive;
                this.sumMetricLong.EndTimeInclusive = dt;
                this.sumMetricLong.IsDeltaTemporality = isDelta;
                if (isDelta)
                {
                    this.startTimeExclusive = dt;
                    this.sumMetricLong.LongSum = this.sumLong - this.lastSumLong;
                    this.lastSumLong = this.sumLong;
                }
                else
                {
                    this.sumMetricLong.LongSum = this.sumLong;
                }
            }

            // TODO: Confirm that this approach of
            // re-using the same instance is correct.
            // This avoids allocating a new instance.
            // It is read only for Exporters,
            // and also there is no parallel
            // Collect allowed.
            return this.sumMetricLong;
        }

        public string ToDisplayString()
        {
            return $"Sum={this.sumLong}";
        }
    }
}
