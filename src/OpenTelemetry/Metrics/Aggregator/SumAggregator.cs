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

#nullable enable

namespace OpenTelemetry.Metrics
{
    public class SumAggregator : Aggregator
    {
        private readonly Instrument instrument;
        private readonly Sequence<string> names;
        private int sum = 0;
        private int count = 0;

        public SumAggregator(Instrument instrument, Sequence<string> names)
        {
            this.instrument = instrument;
            this.names = names;
        }

        public override void Update(DataPoint? value)
        {
            this.count++;

            // TODO: Need to handle DataPoint<T> appropriately instead of using ValueAsString()

            this.sum += int.Parse(value?.ValueAsString());
        }

        public override IEnumerable<Metric> Collect()
        {
            // TODO: Need to determine how to convert to Metric

            if (this.count == 0)
            {
                return Enumerable.Empty<Metric>();
            }

            var attribs = new List<KeyValuePair<string, object?>>();
            string? name = null;
            foreach (var seq in this.names.AsReadOnlySpan())
            {
                if (name == null)
                {
                    name = seq;
                }
                else
                {
                    attribs.Add(new KeyValuePair<string, object?>(name, seq));
                    name = null;
                }
            }

            var tags = new ReadOnlySpan<KeyValuePair<string, object?>>(attribs.ToArray());

            var metrics = new Metric[]
            {
                new Metric(
                    $"{this.instrument.Meter.Name}:{this.instrument.Name}:Count",
                    new DataPoint<int>(this.count, tags)),
                new Metric(
                    $"{this.instrument.Meter.Name}:{this.instrument.Name}:Sum",
                    new DataPoint<int>(this.sum, tags)),
            };

            this.count = 0;
            this.sum = 0;

            return metrics;
        }
    }
}
