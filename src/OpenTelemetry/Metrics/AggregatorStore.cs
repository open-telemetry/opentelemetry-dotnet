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

namespace OpenTelemetry.Metrics
{
    public class AggregatorStore
    {
        private static readonly string[] EmptySeqKey = new string[0];
        private static readonly object[] EmptySeqValue = new object[0];

        private readonly Instrument instrument;
        private readonly MeterProviderSdk sdk;

        // Two-Level lookup. TagKeys x [ TagValues x Aggregators ]
        private readonly Dictionary<string[], Dictionary<object[], Aggregator[]>> keyValue2MetricAggs =
            new Dictionary<string[], Dictionary<object[], Aggregator[]>>(new StringArrayEqualityComparer());

        private Aggregator[] tag0Aggregators = null;

        public AggregatorStore(MeterProviderSdk sdk, Instrument instrument)
        {
            this.sdk = sdk;
            this.instrument = instrument;

            foreach (var processor in this.sdk.AggregateProcessors)
            {
                processor.Register(this);
            }
        }

        internal Aggregator[] GetDefaultAggregator(string[] seqKey, object[] seqVal)
        {
            // TODO: Figure out default aggregator/s based on Instrument and configs

            if (!this.instrument.IsObservable)
            {
                return new Aggregator[]
                {
                    new SumAggregator(this.instrument, seqKey, seqVal),
                };
            }

            return new Aggregator[]
            {
                new LastValueAggregator(this.instrument, seqKey, seqVal),
            };
        }

        internal Aggregator[] FindAggregators(KeyValuePair<string, object>[] tags)
        {
            int len = tags.Length;

            if (len == 0)
            {
                if (this.tag0Aggregators == null)
                {
                    this.tag0Aggregators = this.GetDefaultAggregator(AggregatorStore.EmptySeqKey, AggregatorStore.EmptySeqValue);
                }

                return this.tag0Aggregators;
            }

            var storage = ThreadStaticStorage.GetStorage();

            storage.GetKeysValues(tags, out var tagKey, out var tagValue);

            if (len > 1)
            {
                Array.Sort<string, object>(tagKey, tagValue);
            }

            string[] seqKey = null;

            // GetOrAdd by TagKey at 1st Level of 2-level dictionary structure.
            // Get back a Dictionary of [ Values x Aggregators[] ].
            if (!this.keyValue2MetricAggs.TryGetValue(tagKey, out var value2metrics))
            {
                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.

                seqKey = new string[len];
                tagKey.CopyTo(seqKey, 0);

                value2metrics = new Dictionary<object[], Aggregator[]>(new ObjectArrayEqualityComparer());
                this.keyValue2MetricAggs.Add(seqKey, value2metrics);
            }

            // GetOrAdd by TagValue at 2st Level of 2-level dictionary structure.
            // Get back Aggregators[].
            if (!value2metrics.TryGetValue(tagValue, out var aggregators))
            {
                // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.

                if (seqKey == null)
                {
                    seqKey = new string[len];
                    tagKey.CopyTo(seqKey, 0);
                }

                var seqVal = new object[len];
                tagValue.CopyTo(seqVal, 0);

                aggregators = this.GetDefaultAggregator(seqKey, seqVal);

                value2metrics.Add(seqVal, aggregators);
            }

            return aggregators;
        }

        internal void Update(IDataPoint point)
        {
            var aggs = this.FindAggregators(point.Tags);

            foreach (var agg in aggs)
            {
                agg.Update(point);
            }
        }

        internal List<Metric> Collect()
        {
            var aggs = new List<Aggregator>();

            foreach (var keys in this.keyValue2MetricAggs)
            {
                foreach (var values in keys.Value)
                {
                    aggs.AddRange(values.Value);
                }
            }

            var metrics = new List<Metric>();

            foreach (var aggregator in aggs)
            {
                metrics.AddRange(aggregator.Collect());
            }

            return metrics;
        }
    }
}
