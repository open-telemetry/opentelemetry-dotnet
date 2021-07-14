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
using System.Linq;

namespace OpenTelemetry.Metrics
{
    internal class AggregatorStore
    {
        private static readonly string[] EmptySeqKey = new string[0];
        private static readonly object[] EmptySeqValue = new object[0];

        private readonly Instrument instrument;
        private readonly MeterProviderSdk sdk;

        private readonly object lockKeyValue2MetricAggs = new object();

        // Two-Level lookup. TagKeys x [ TagValues x Metrics ]
        private readonly Dictionary<string[], Dictionary<object[], MetricAgg[]>> keyValue2MetricAggs =
            new Dictionary<string[], Dictionary<object[], MetricAgg[]>>(new StringArrayEqualityComparer());

        private MetricAgg[] tag0Metrics = null;

        private IEnumerable<int> timePeriods;

        internal AggregatorStore(MeterProviderSdk sdk, Instrument instrument)
        {
            this.sdk = sdk;
            this.instrument = instrument;

            this.timePeriods = this.sdk.ExportProcessors.Select(k => k.Value).Distinct();
        }

        internal MetricAgg[] MapToMetrics(string[] seqKey, object[] seqVal)
        {
            var metricpairs = new List<MetricAgg>();

            var name = $"{this.instrument.Meter.Name}:{this.instrument.Name}";

            var tags = new KeyValuePair<string, object>[seqKey.Length];
            for (int i = 0; i < seqKey.Length; i++)
            {
                tags[i] = new KeyValuePair<string, object>(seqKey[i], seqVal[i]);
            }

            var dt = DateTimeOffset.UtcNow;

            foreach (var timeperiod in this.timePeriods)
            {
                // TODO: Need to map each instrument to metrics (based on View API)

                if (this.instrument.GetType().Name.Contains("Counter"))
                {
                    metricpairs.Add(new MetricAgg(timeperiod, new SumMetricAggregator(name, dt, tags, true)));
                }
                else if (this.instrument.GetType().Name.Contains("Gauge"))
                {
                    metricpairs.Add(new MetricAgg(timeperiod, new GaugeMetricAggregator(name, dt, tags)));
                }
                else if (this.instrument.GetType().Name.Contains("Histogram"))
                {
                    metricpairs.Add(new MetricAgg(timeperiod, new HistogramMetricAggregator(name, dt, tags, false)));
                }
                else
                {
                    metricpairs.Add(new MetricAgg(timeperiod, new SummaryMetricAggregator(name, dt, tags, false)));
                }
            }

            return metricpairs.ToArray();
        }

        internal MetricAgg[] FindMetricAggregators(ReadOnlySpan<KeyValuePair<string, object>> tags)
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

            MetricAgg[] metrics;

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

                    value2metrics = new Dictionary<object[], MetricAgg[]>(new ObjectArrayEqualityComparer());
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

        internal void Update<T>(DateTimeOffset dt, T value, ReadOnlySpan<KeyValuePair<string, object>> tags)
            where T : struct
        {
            // TODO: We can isolate the cost of each user-added aggregator in
            // the hot path by queuing the DataPoint, and doing the Update as
            // part of the Collect() instead. Thus, we only pay for the price
            // of queueing a DataPoint in the Hot Path

            var metricPairs = this.FindMetricAggregators(tags);

            foreach (var pair in metricPairs)
            {
                pair.Metric.Update(dt, value);
            }
        }

        internal List<IMetric> Collect(int periodMilliseconds)
        {
            var collectedMetrics = new List<IMetric>();

            var dt = DateTimeOffset.UtcNow;

            foreach (var keys in this.keyValue2MetricAggs)
            {
                foreach (var values in keys.Value)
                {
                    foreach (var metric in values.Value)
                    {
                        if (metric.TimePeriod == periodMilliseconds)
                        {
                            var m = metric.Metric.Collect(dt);
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

        internal class MetricAgg
        {
            internal int TimePeriod;
            internal IAggregator Metric;

            internal MetricAgg(int timePeriod, IAggregator metric)
            {
                this.TimePeriod = timePeriod;
                this.Metric = metric;
            }
        }
    }
}
