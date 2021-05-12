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
        private static readonly Sequence<string> EmptySeq = new Sequence<string>(new string[0]);

        private readonly Instrument instrument;
        private readonly MeterProviderSdk sdk;

        private readonly object lockMetricAggs = new object();
        private readonly Dictionary<ISequence, Aggregator[]> metricAggs = new Dictionary<ISequence, Aggregator[]>();

        private readonly List<Aggregator> aggregators = new List<Aggregator>(100);

        public AggregatorStore(MeterProviderSdk sdk, Instrument instrument)
        {
            this.sdk = sdk;
            this.instrument = instrument;

            foreach (var processor in this.sdk.AggregateProcessors)
            {
                processor.Register(this);
            }
        }

        internal void Update(IDataPoint point)
        {
            // TODO: View API to configure which Tag/s to use.

            this.aggregators.Clear();

            lock (this.lockMetricAggs)
            {
                Sequence<string> seq = AggregatorStore.EmptySeq;
                if (!this.metricAggs.TryGetValue(seq, out var aggs))
                {
                    aggs = new Aggregator[]
                    {
                        new SumAggregator(this.instrument, seq),
                        new LastValueAggregator(this.instrument, seq),
                    };

                    this.metricAggs.Add(AggregatorStore.EmptySeq, aggs);
                }

                this.aggregators.AddRange(aggs);
            }

            foreach (var agg in this.aggregators)
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
