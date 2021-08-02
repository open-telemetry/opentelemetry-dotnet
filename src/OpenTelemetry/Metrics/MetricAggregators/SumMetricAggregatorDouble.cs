// <copyright file="SumMetricAggregatorDouble.cs" company="OpenTelemetry Authors">
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
    internal class SumMetricAggregatorDouble : IAggregator
    {
        private readonly object lockUpdate = new object();
        private double sumDouble = 0;
        private SumMetricDouble sumMetricDouble;
        private DateTimeOffset startTimeExclusive;

        internal SumMetricAggregatorDouble(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes)
        {
            this.startTimeExclusive = startTimeExclusive;
            this.sumMetricDouble = new SumMetricDouble(name, description, unit, meter, startTimeExclusive, attributes);
        }

        public void Update<T>(T value)
            where T : struct
        {
            // TODO: Replace Lock with
            // TryAdd..{Spin..TryAdd..Repeat} if "lost race to another thread"
            lock (this.lockUpdate)
            {
                if (typeof(T) == typeof(double))
                {
                    // TODO: Confirm this doesn't cause boxing.
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
            lock (this.lockUpdate)
            {
                this.sumMetricDouble.StartTimeExclusive = this.startTimeExclusive;
                this.sumMetricDouble.EndTimeInclusive = dt;
                this.sumMetricDouble.DoubleSum = this.sumDouble;
                this.sumMetricDouble.IsDeltaTemporality = isDelta;
                if (isDelta)
                {
                    this.startTimeExclusive = dt;
                    this.sumDouble = 0;
                }
            }

            // TODO: Confirm that this approach of
            // re-using the same instance is correct.
            // This avoids allocating a new instance.
            // It is read only for Exporters,
            // and also there is no parallel
            // Collect allowed.
            return this.sumMetricDouble;
        }

        public string ToDisplayString()
        {
            return $"Sum={this.sumDouble}";
        }
    }
}
