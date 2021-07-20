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

        // Two-Level lookup. [ NameAndKeys ] x [ [ TagValues ] x [ Metrics[] ] ]
        // First Level: Lookup by NameAndKeys (name + TagKeys). Return 2nd Level dictionary
        // Second Level: Lookup by TagValues. Return Array of MetricAggs
        private readonly Dictionary<NameAndKeys, Dictionary<object[], IAggregator[]>> keyValue2MetricAggs =
            new Dictionary<NameAndKeys, Dictionary<object[], IAggregator[]>>(new NameAndKeysEqualityComparer());

        private IAggregator[] tag0Metrics = null;

        internal AggregatorStore(MeterProviderSdk sdk, Instrument instrument)
        {
            this.sdk = sdk;
            this.instrument = instrument;
        }

        internal IAggregator[] MapToMetrics(string viewname, Func<IAggregator[]> aggregators, string[] seqKey, object[] seqVal)
        {
            var metricpairs = new List<IAggregator>();

            var tags = new KeyValuePair<string, object>[seqKey.Length];
            for (int i = 0; i < seqKey.Length; i++)
            {
                tags[i] = new KeyValuePair<string, object>(seqKey[i], seqVal[i]);
            }

            var dt = DateTimeOffset.UtcNow;

            var name = $"{this.instrument.Meter.Name}:{viewname}";

            if (aggregators == null)
            {
                var instType = this.instrument.GetType().Name;

                if (instType.StartsWith("Counter"))
                {
                    aggregators = () =>
                    {
                        return new IAggregator[]
                        {
                            new SumMetricAggregator(true, false),
                        };
                    };
                }
                else if (instType.StartsWith("ObservableCounter"))
                {
                    aggregators = () =>
                    {
                        return new IAggregator[]
                        {
                            new SumMetricAggregator(false, false),
                        };
                    };
                }
                else if (instType.StartsWith("ObservableGauge"))
                {
                    aggregators = () =>
                    {
                        return new IAggregator[]
                        {
                            new GaugeMetricAggregator(),
                        };
                    };
                }
                else if (instType.StartsWith("Histogram"))
                {
                    aggregators = () =>
                    {
                        return new IAggregator[]
                        {
                            new HistogramMetricAggregator(true, new double[] { 0.0 }),
                        };
                    };
                }
                else
                {
                    aggregators = () =>
                    {
                        return new IAggregator[]
                        {
                            new SummaryMetricAggregator(),
                        };
                    };
                }
            }

            var aggs = aggregators();
            foreach (var agg in aggs)
            {
                agg.Init(name, this.instrument.Description, this.instrument.Unit, this.instrument.Meter, dt, tags);
                metricpairs.Add(agg);
            }

            return metricpairs.ToArray();
        }

        internal IAggregator[] FindMetricAggregators(
            ThreadStaticStorage storage,
            string name,
            Func<IAggregator[]> aggregators,
            ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            int len = tags.Length;

            if (len == 0)
            {
                if (this.tag0Metrics == null)
                {
                    this.tag0Metrics = this.MapToMetrics(name, aggregators, AggregatorStore.EmptySeqKey, AggregatorStore.EmptySeqValue);
                }

                return this.tag0Metrics;
            }

            storage.SplitToKeysAndValues(tags, out var tagKey, out var tagValue);

            if (len > 1)
            {
                Array.Sort<string, object>(tagKey, tagValue);
            }

            IAggregator[] metrics;

            lock (this.lockKeyValue2MetricAggs)
            {
                string[] seqKey = null;

                var localKey = storage.LocalNameAndKeys;
                localKey.Name = name;
                localKey.Keys = tagKey;

                // GetOrAdd by NameAndKeys(name, TagKey) at 1st Level of 2-level dictionary structure.
                // Get back a Dictionary of [ TagValues x Metrics[] ].
                if (!this.keyValue2MetricAggs.TryGetValue(localKey, out var value2metrics))
                {
                    // Note: We are using storage from ThreadStatic, so need to make a deep copy for Dictionary storage.

                    seqKey = new string[len];
                    tagKey.CopyTo(seqKey, 0);

                    var cloneKey = new AggregatorStore.NameAndKeys(name, seqKey);

                    value2metrics = new Dictionary<object[], IAggregator[]>(new ObjectArrayEqualityComparer());
                    this.keyValue2MetricAggs.Add(cloneKey, value2metrics);
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

                    metrics = this.MapToMetrics(name, aggregators, seqKey, seqVal);

                    value2metrics.Add(seqVal, metrics);
                }
            }

            return metrics;
        }

        internal void Update<T>(T value, ReadOnlySpan<KeyValuePair<string, object>> tags)
            where T : struct
        {
            var storage = ThreadStaticStorage.GetStorage();

            var hasMatchingInstrument = false;

            if (this.sdk.MetricViews != null && this.sdk.MetricViews.Length > 0)
            {
                // Build all Views

                storage.ViewBuilder.Clear();

                foreach (var view in this.sdk.MetricViews)
                {
                    if (storage.ViewBuilder.ApplyView(view, this.instrument, tags))
                    {
                        hasMatchingInstrument = true;
                    }
                }
            }

            if (hasMatchingInstrument)
            {
                var count = storage.ViewBuilder.Count;
                for (int pos = 0; pos < count; pos++)
                {
                    var ros = storage.ViewBuilder.GetViewAt(pos, out var view);

                    var metricPairs = this.FindMetricAggregators(storage, view.Name ?? this.instrument.Name, view.Aggregators, ros);

                    foreach (var pair in metricPairs)
                    {
                        pair.Update(value);
                    }
                }
            }
            else
            {
                // No Views defined

                var metricPairs = this.FindMetricAggregators(storage, this.instrument.Name, null, tags);

                foreach (var pair in metricPairs)
                {
                    pair.Update(value);
                }
            }
        }

        internal List<IMetric> Collect(bool isDelta)
        {
            var collectedMetrics = new List<IMetric>();

            var dt = DateTimeOffset.UtcNow;

            foreach (var keys in this.keyValue2MetricAggs)
            {
                foreach (var values in keys.Value)
                {
                    foreach (var metric in values.Value)
                    {
                        var m = metric.Collect(dt);
                        if (m != null)
                        {
                            collectedMetrics.Add(m);
                        }
                    }
                }
            }

            return collectedMetrics;
        }

        internal class NameAndKeys
        {
            internal NameAndKeys(string name, string[] keys)
            {
                this.Name = name;
                this.Keys = keys;
            }

            internal string Name { get; set; }

            internal string[] Keys { get; set; }
        }
    }
}
