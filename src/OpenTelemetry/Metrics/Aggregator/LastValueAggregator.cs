// <copyright file="LastValueAggregator.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Metrics
{
    public class LastValueAggregator : Aggregator
    {
        private readonly Instrument instrument;
        private readonly KeyValuePair<string, object>[] tags;

        private readonly object lockUpdate = new object();
        private int count = 0;
        private DataPoint lastDataPoint;

        public LastValueAggregator(Instrument instrument, string[] names, object[] values)
        {
            this.instrument = instrument;

            if (names.Length != values.Length)
            {
                throw new ArgumentException($"Length of {nameof(names)} and {nameof(values)} must match.");
            }

            this.tags = new KeyValuePair<string, object>[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                this.tags[i] = new KeyValuePair<string, object>(names[i], values[i]);
            }
        }

        public override void Update<T>(DateTimeOffset dt, T value)
            where T : struct
        {
            lock (this.lockUpdate)
            {
                this.count++;
                this.lastDataPoint = DataPoint.CreateDataPoint(dt, value, this.tags);
            }
        }

        public override IEnumerable<Metric> Collect()
        {
            // TODO: Need to determine how to convert to Metric

            if (this.count == 0)
            {
                return Enumerable.Empty<Metric>();
            }

            DataPoint lastValue;
            lock (this.lockUpdate)
            {
                lastValue = this.lastDataPoint;
                this.count = 0;
            }

            var metrics = new Metric[]
            {
                new Metric(
                    $"{this.instrument.Meter.Name}:{this.instrument.Name}:LastValue",
                    lastValue),
            };

            return metrics;
        }
    }
}
