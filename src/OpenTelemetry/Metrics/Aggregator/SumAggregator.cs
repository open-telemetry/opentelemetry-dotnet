// <copyright file="SumAggregator.cs" company="OpenTelemetry Authors">
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
    public class SumAggregator : Aggregator
    {
        private readonly Instrument instrument;
        private readonly string[] names;
        private readonly object[] values;
        private long sum = 0;
        private long count = 0;

        public SumAggregator(Instrument instrument, string[] names, object[] values)
        {
            if (names.Length != values.Length)
            {
                throw new ArgumentException("Length of names[] and values[] must match.");
            }

            this.instrument = instrument;
            this.names = names;
            this.values = values;
        }

        public override void Update(IDataPoint value)
        {
            this.count++;

            // TODO: Need to handle DataPoint<T> appropriately

            if (value is DataPoint<int> intV)
            {
                this.sum += intV.Value;
            }
            else if (value is DataPoint<long> longV)
            {
                this.sum += longV.Value;
            }
            else
            {
                throw new Exception("Unsupported Type");
            }
        }

        public override IEnumerable<Metric> Collect()
        {
            // TODO: Need to determine how to convert to Metric

            if (this.count == 0)
            {
                return Enumerable.Empty<Metric>();
            }

            var attribs = new List<KeyValuePair<string, object>>();
            for (int i = 0; i < this.names.Length; i++)
            {
                attribs.Add(new KeyValuePair<string, object>(this.names[i], this.values[i]));
            }

            var tags = attribs.ToArray();

            var metrics = new Metric[]
            {
                new Metric(
                    $"{this.instrument.Meter.Name}:{this.instrument.Name}:Count",
                    new DataPoint<int>((int)this.count, tags)),
                new Metric(
                    $"{this.instrument.Meter.Name}:{this.instrument.Name}:Sum",
                    new DataPoint<int>((int)this.sum, tags)),
            };

            this.count = 0;
            this.sum = 0;

            return metrics;
        }
    }
}
