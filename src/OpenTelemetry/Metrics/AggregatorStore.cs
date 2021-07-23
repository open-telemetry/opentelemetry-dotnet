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
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    internal class AggregatorStore
    {
        private static readonly string[] EmptySeqKey = new string[0];
        private static readonly object[] EmptySeqValue = new object[0];
        private readonly Instrument instrument;
        private readonly object lockKeyValue2MetricAggs = new object();

        // Two-Level lookup. TagKeys x [ TagValues x Metrics ]
        private readonly Dictionary<string[], Dictionary<object[], IAggregator[]>> keyValue2MetricAggs =
            new Dictionary<string[], Dictionary<object[], IAggregator[]>>(new StringArrayEqualityComparer());

        private IAggregator[] tag0Metrics = null;

        internal AggregatorStore(Instrument instrument)
        {
            this.instrument = instrument;
        }

        internal IAggregator[] MapToMetrics(string[] seqKey, object[] seqVal)
        {
            var aggregators = new List<IAggregator>();

            var tags = new KeyValuePair<string, object>[seqKey.Length];
            for (int i = 0; i < seqKey.Length; i++)
            {
                tags[i] = new KeyValuePair<string, object>(seqKey[i], seqVal[i]);
            }

            var dt = DateTimeOffset.UtcNow;

            // TODO: Need to map each instrument to metrics (based on View API)
            if (this.instrument.GetType().Name.Contains("Counter"))
            {
                aggregators.Add(new SumMetricAggregator(this.instrument.Name, this.instrument.Description, this.instrument.Unit, this.instrument.Meter, dt, tags));
            }
            else if (this.instrument.GetType().Name.Contains("Gauge"))
            {
                aggregators.Add(new GaugeMetricAggregator(this.instrument.Name, this.instrument.Description, this.instrument.Unit, this.instrument.Meter, dt, tags));
            }
            else if (this.instrument.GetType().Name.Contains("Histogram"))
            {
                aggregators.Add(new HistogramMetricAggregator(this.instrument.Name, this.instrument.Description, this.instrument.Unit, this.instrument.Meter, dt, tags));
            }
            else
            {
                aggregators.Add(new SummaryMetricAggregator(this.instrument.Name, this.instrument.Description, this.instrument.Unit, this.instrument.Meter, dt, tags, false));
            }

            return aggregators.ToArray();
        }

        internal IAggregator[] FindMetricAggregators(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            int len = tags.Length;

            if (len == 0)
            {
                if (this.tag0Metrics == null)
                {
                    this.tag0Metrics = this.MapToMetrics(AggregatorStore.EmptySeqKey, AggregatorStore.EmptySeqValue);
                }

                return this.tag0Metrics;
            }

            var storage = ThreadStaticStorage.GetStorage();

            storage.SplitToKeysAndValues(tags, out var tagKey, out var tagValue);

            if (len > 1)
            {
                Array.Sort<string, object>(tagKey, tagValue);
            }

            IAggregator[] metrics;

            lock (this.lockKeyValue2MetricAggs)
            {
                string[] seqKey = null;

                // GetOrAdd by TagKey at 1st Level of 2-level dictionary structure.
                // Get back a Dictionary of [ Values x Metrics[] ].
                if (!this.keyValue2MetricAggs.TryGetValue(tagKey, out var value2metrics))
                {
                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.

                    seqKey = new string[len];
                    tagKey.CopyTo(seqKey, 0);

                    value2metrics = new Dictionary<object[], IAggregator[]>(new ObjectArrayEqualityComparer());
                    this.keyValue2MetricAggs.Add(seqKey, value2metrics);
                }

                // GetOrAdd by TagValue at 2st Level of 2-level dictionary structure.
                // Get back Metrics[].
                if (!value2metrics.TryGetValue(tagValue, out metrics))
                {
                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.

                    if (seqKey == null)
                    {
                        seqKey = new string[len];
                        tagKey.CopyTo(seqKey, 0);
                    }

                    var seqVal = new object[len];
                    tagValue.CopyTo(seqVal, 0);

                    metrics = this.MapToMetrics(seqKey, seqVal);

                    value2metrics.Add(seqVal, metrics);
                }
            }

            return metrics;
        }

        internal void Update<T>(T value, ReadOnlySpan<KeyValuePair<string, object>> tags)
            where T : struct
        {
            // TODO: We can isolate the cost of each user-added aggregator in
            // the hot path by queuing the DataPoint, and doing the Update as
            // part of the Collect() instead. Thus, we only pay for the price
            // of queueing a DataPoint in the Hot Path

            var metricAggregators = this.FindMetricAggregators(tags);

            foreach (var metricAggregator in metricAggregators)
            {
                metricAggregator.Update(value);
            }
        }

        internal List<IMetric> Collect(bool isDelta, DateTimeOffset dt)
        {
            var collectedMetrics = new List<IMetric>();

            // Lock to prevent new time series from being added
            // until collect is done.
            lock (this.lockKeyValue2MetricAggs)
            {
                foreach (var keys in this.keyValue2MetricAggs)
                {
                    foreach (var values in keys.Value)
                    {
                        foreach (var metric in values.Value)
                        {
                            var m = metric.Collect(dt, isDelta);
                            if (m != null)
                            {
                                collectedMetrics.Add(m);
                            }
                        }
                    }
                }
            }

            return collectedMetrics;
        }
    }
}
