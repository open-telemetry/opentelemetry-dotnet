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
        private readonly Sequence<string> names;
        private IDataPoint lastValue = null;
        private int count = 0;

        public LastValueAggregator(Instrument instrument, Sequence<string> names)
        {
            this.instrument = instrument;
            this.names = names;
        }

        public override void Update(IDataPoint value)
        {
            this.count++;
            this.lastValue = value;
        }

        public override IEnumerable<Metric> Collect()
        {
            // TODO: Need to determine how to convert to Metric

            if (this.count == 0)
            {
                return Enumerable.Empty<Metric>();
            }

            var attribs = new List<KeyValuePair<string, object>>();
            string name = null;
            foreach (var seq in this.names.Values)
            {
                if (name == null)
                {
                    name = seq;
                }
                else
                {
                    attribs.Add(new KeyValuePair<string, object>(name, seq));
                    name = null;
                }
            }

            var dp = this.lastValue?.NewWithTags(attribs.ToArray());

            var metrics = new Metric[]
            {
                new Metric(
                    $"{this.instrument.Meter.Name}:{this.instrument.Name}:LastValue",
                    dp),
            };

            this.count = 0;

            return metrics;
        }
    }
}
