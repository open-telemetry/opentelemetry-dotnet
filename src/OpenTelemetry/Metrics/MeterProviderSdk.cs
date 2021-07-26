// <copyright file="MeterProviderSdk.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    public class MeterProviderSdk
        : MeterProvider
    {
        internal readonly ConcurrentDictionary<AggregatorStore, bool> AggregatorStores = new ConcurrentDictionary<AggregatorStore, bool>();

        private readonly List<object> instrumentations = new List<object>();
        private readonly object collectLock = new object();
        private readonly MeterListener listener;
        private readonly List<MetricProcessor> metricProcessors = new List<MetricProcessor>();

        internal MeterProviderSdk(
            Resource resource,
            IEnumerable<string> meterSources,
            List<MeterProviderBuilderSdk.InstrumentationFactory> instrumentationFactories,
            MetricProcessor[] metricProcessors)
        {
            this.Resource = resource;

            // TODO: Replace with single CompositeProcessor.
            this.metricProcessors.AddRange(metricProcessors);

            foreach (var processor in this.metricProcessors)
            {
                processor.SetGetMetricFunction(this.Collect);
                processor.SetParentProvider(this);
            }

            if (instrumentationFactories.Any())
            {
                foreach (var instrumentationFactory in instrumentationFactories)
                {
                    this.instrumentations.Add(instrumentationFactory.Factory());
                }
            }

            // Setup Listener
            var meterSourcesToSubscribe = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in meterSources)
            {
                meterSourcesToSubscribe[name] = true;
            }

            this.listener = new MeterListener()
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (meterSourcesToSubscribe.ContainsKey(instrument.Meter.Name))
                    {
                        var aggregatorStore = new AggregatorStore(instrument);

                        // Lock to prevent new instrument (aggregatorstore)
                        // from being added while Collect is going on.
                        lock (this.collectLock)
                        {
                            this.AggregatorStores.TryAdd(aggregatorStore, true);
                            listener.EnableMeasurementEvents(instrument, aggregatorStore);
                        }
                    }
                },
                MeasurementsCompleted = (instrument, state) => this.MeasurementsCompleted(instrument, state),
            };

            // Everything double
            this.listener.SetMeasurementEventCallback<double>((i, m, l, c) => this.MeasurementRecorded(i, m, l, c));
            this.listener.SetMeasurementEventCallback<float>((i, m, l, c) => this.MeasurementRecorded(i, (double)m, l, c));

            // Everything long
            this.listener.SetMeasurementEventCallback<long>((i, m, l, c) => this.MeasurementRecorded(i, m, l, c));
            this.listener.SetMeasurementEventCallback<int>((i, m, l, c) => this.MeasurementRecorded(i, (long)m, l, c));
            this.listener.SetMeasurementEventCallback<short>((i, m, l, c) => this.MeasurementRecorded(i, (long)m, l, c));
            this.listener.SetMeasurementEventCallback<byte>((i, m, l, c) => this.MeasurementRecorded(i, (long)m, l, c));

            this.listener.Start();
        }

        internal Resource Resource { get; }

        internal void MeasurementsCompleted(Instrument instrument, object state)
        {
            Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} completed.");
        }

        internal void MeasurementRecorded<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
            where T : struct
        {
            // Get Instrument State
            var aggregatorStore = state as AggregatorStore;

            if (instrument == null || aggregatorStore == null)
            {
                // TODO: log
                return;
            }

            aggregatorStore.Update(value, tagsRos);
        }

        protected override void Dispose(bool disposing)
        {
            if (this.instrumentations != null)
            {
                foreach (var item in this.instrumentations)
                {
                    (item as IDisposable)?.Dispose();
                }

                this.instrumentations.Clear();
            }

            foreach (var processor in this.metricProcessors)
            {
                processor.Dispose();
            }

            this.listener.Dispose();
        }

        private MetricItem Collect(bool isDelta)
        {
            lock (this.collectLock)
            {
                MetricItem metricItem = null;
                try
                {
                    // Record all observable instruments
                    this.listener.RecordObservableInstruments();
                    var dt = DateTimeOffset.UtcNow;
                    metricItem = new MetricItem();
                    foreach (var kv in this.AggregatorStores)
                    {
                        var metrics = kv.Key.Collect(isDelta, dt);
                        metricItem.Metrics.AddRange(metrics);
                    }
                }
                catch (Exception)
                {
                    // TODO: Log
                }

                return metricItem;
            }
        }
    }
}
