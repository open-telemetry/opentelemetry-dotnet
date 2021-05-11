// <copyright file="AggregatorStore.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

#nullable enable

namespace OpenTelemetry.Metrics
{
    public class AggregatorStore
    {
        private readonly Instrument instrument;
        private readonly MeterProviderSdk sdk;
        private readonly ConcurrentDictionary<ISequence, Aggregator[]> aggs = new ConcurrentDictionary<ISequence, Aggregator[]>();

        public AggregatorStore(MeterProviderSdk sdk, Instrument instrument)
        {
            this.sdk = sdk;
            this.instrument = instrument;

            foreach (var processor in this.sdk.AggregateProcessors)
            {
                processor.Register(this);
            }
        }

        internal void Update(DataPoint? point)
        {
            var aggs = this.GetAggregators(point!.Tags);
            foreach (var agg in aggs)
            {
                agg.Update(point);
            }
        }

        internal List<Metric> Collect()
        {
            var metrics = new List<Metric>();

            foreach (var kv in this.aggs)
            {
                foreach (var aggregator in kv.Value)
                {
                    metrics.AddRange(aggregator.Collect());
                }
            }

            return metrics;
        }

        internal List<Aggregator> GetAggregators(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var aggregators = new List<Aggregator>();

            // TODO: Expect View API to configure which Tag/s to use.

            // Dropping all tags
            {
                var tagNames = new string[0];
                var seq0 = new Sequence<string>(tagNames);
                this.SetupNewAggregators(aggregators, seq0, (seq) =>
                    new Aggregator[]
                    {
                        new SumAggregator(this.instrument, seq),
                        new LastValueAggregator(this.instrument, seq),
                    });
            }

            // Group tag (ignore value) 1D at a time.
            foreach (var tag in tags)
            {
                var tagNames = new string[]
                {
                    tag.Key,
                    "*",
                };
                var seq1 = new Sequence<string>(tagNames);

                this.SetupNewAggregators(aggregators, seq1, (seq) =>
                    new Aggregator[]
                    {
                        new SumAggregator(this.instrument, seq),
                    });
            }

            // Group tag + value 1D at a time.
            foreach (var tag in tags)
            {
                var tagNames = new string[]
                {
                    tag.Key,
                    tag.Value!.ToString(),
                };
                var seq1 = new Sequence<string>(tagNames);

                this.SetupNewAggregators(aggregators, seq1, (seq) =>
                    new Aggregator[]
                    {
                        new SumAggregator(this.instrument, seq),
                    });
            }

            return aggregators;
        }

        private void SetupNewAggregators(List<Aggregator> aggregators, Sequence<string> seq, Func<Sequence<string>, Aggregator[]> func)
        {
            var aggs = this.aggs.GetOrAdd(seq, (k) =>
            {
                return func(seq);
            });

            aggregators.AddRange(aggs);
        }
    }
}
