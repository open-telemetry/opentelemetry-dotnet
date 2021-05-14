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

        [ThreadStatic]
        private static string[] tag1KeyTemp;

        [ThreadStatic]
        private static object[] tag1ValueTemp;

        [ThreadStatic]
        private static string[] tag2KeyTemp;

        [ThreadStatic]
        private static object[] tag2ValueTemp;

        [ThreadStatic]
        private static string[] tag3KeyTemp;

        [ThreadStatic]
        private static object[] tag3ValueTemp;

        private readonly Instrument instrument;
        private readonly MeterProviderSdk sdk;

        // Two Level lookup. Keys x Values = Aggregators
        private readonly Dictionary<string[], Dictionary<object[], Aggregator[]>> keyValue2MetricAggs =
            new Dictionary<string[], Dictionary<object[], Aggregator[]>>(new StringArrayEquaityComparer());

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

        internal Aggregator[] GetAggregator(string[] seqKey, object[] seqVal)
        {
            return new Aggregator[]
            {
                new SumAggregator(this.instrument, seqKey, seqVal),
                new LastValueAggregator(this.instrument, seqKey, seqVal),
            };
        }

        internal void Update(IDataPoint point)
        {
            this.InitThreadLocal();

            Aggregator[] aggs;

            int len = point.Tags.Length;

            if (len == 0)
            {
                aggs = this.tag0Aggregators;
            }
            else
            {
                string[] tagKeyTemp;
                object[] tagValueTemp;

                if (len == 1)
                {
                    tagKeyTemp = AggregatorStore.tag1KeyTemp;
                    tagValueTemp = AggregatorStore.tag1ValueTemp;
                }
                else if (len == 2)
                {
                    tagKeyTemp = AggregatorStore.tag2KeyTemp;
                    tagValueTemp = AggregatorStore.tag2ValueTemp;
                }
                else if (len == 3)
                {
                    tagKeyTemp = AggregatorStore.tag3KeyTemp;
                    tagValueTemp = AggregatorStore.tag3ValueTemp;
                }
                else
                {
                    tagKeyTemp = new string[len];
                    tagValueTemp = new object[len];
                }

                int i = 0;
                foreach (var tag in point.SortedTags)
                {
                    tagKeyTemp[i] = tag.Key;
                    tagValueTemp[i] = tag.Value;
                    i++;
                }

                // Two-Level lookup of Key and Value to get Aggregator[]

                if (!this.keyValue2MetricAggs.TryGetValue(tagKeyTemp, out var value2metrics))
                {
                    var seq = new string[tagKeyTemp.Length];
                    tagKeyTemp.CopyTo(seq, 0);

                    value2metrics = new Dictionary<object[], Aggregator[]>(new ObjectArrayEquaityComparer());
                    this.keyValue2MetricAggs.Add(seq, value2metrics);
                }

                if (!value2metrics.TryGetValue(tagValueTemp, out aggs))
                {
                    var seqKey = new string[tagKeyTemp.Length];
                    tagKeyTemp.CopyTo(seqKey, 0);

                    var seqVal = new object[tagValueTemp.Length];
                    tagValueTemp.CopyTo(seqVal, 0);

                    aggs = this.GetAggregator(seqKey, seqVal);
                    value2metrics.Add(seqVal, aggs);
                }
            }

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

        private void InitThreadLocal()
        {
            if (this.tag0Aggregators == null)
            {
                this.tag0Aggregators = this.GetAggregator(AggregatorStore.EmptySeqKey, AggregatorStore.EmptySeqValue);
            }

            if (AggregatorStore.tag1KeyTemp == null)
            {
                AggregatorStore.tag1KeyTemp = new string[1];
                AggregatorStore.tag1ValueTemp = new object[1];

                AggregatorStore.tag2KeyTemp = new string[2];
                AggregatorStore.tag2ValueTemp = new object[2];

                AggregatorStore.tag3KeyTemp = new string[3];
                AggregatorStore.tag3ValueTemp = new object[3];
            }
        }
    }
}
