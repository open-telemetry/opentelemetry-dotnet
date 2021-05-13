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
        private static readonly Sequence<string> EmptySeq = new Sequence<string>(new string[0]);

        private readonly Instrument instrument;
        private readonly MeterProviderSdk sdk;

        private readonly object lockMetricAggs = new object();
        private readonly Dictionary<ISequence, Aggregator[]> metricAggs = new Dictionary<ISequence, Aggregator[]>();

        private readonly List<Aggregator> aggregators = new List<Aggregator>(100);

        private readonly string[] tag0Temp = new string[0];
        private readonly string[] tag1Temp = new string[2];
        private readonly string[] tag2Temp = new string[4];
        private readonly string[] tag3Temp = new string[6];

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

        internal Aggregator[] GetAggregator(Sequence<string> seq)
        {
            return new Aggregator[]
            {
                new SumAggregator(this.instrument, seq),
                new LastValueAggregator(this.instrument, seq),
            };
        }

        internal void Update(IDataPoint point)
        {
            Aggregator[] aggs;

            int len = point.TagsAsArray.Length;

            if (len == 0)
            {
                if (this.tag0Aggregators == null)
                {
                    this.tag0Aggregators = this.GetAggregator(AggregatorStore.EmptySeq);
                }

                aggs = this.tag0Aggregators;
            }
            else
            {
                Sequence<string> seq;
                string[] tagKeyValues;

                if (len == 1)
                {
                    tagKeyValues = this.tag1Temp;
                }
                else if (len == 2)
                {
                    tagKeyValues = this.tag2Temp;
                }
                else if (len == 3)
                {
                    tagKeyValues = this.tag3Temp;
                }
                else
                {
                    tagKeyValues = new string[2 * len];
                }

                int i = 0;
                foreach (var tag in point.TagsAsArray)
                {
                    tagKeyValues[i++] = tag.Key;
                    tagKeyValues[i++] = tag.Value.ToString();
                }

                seq = new Sequence<string>(tagKeyValues);

                if (!this.metricAggs.TryGetValue(seq, out aggs))
                {
                    aggs = this.GetAggregator(seq);
                    this.metricAggs.Add(seq, aggs);
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

            lock (this.lockMetricAggs)
            {
                foreach (var kv in this.metricAggs)
                {
                    aggs.AddRange(kv.Value);
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
