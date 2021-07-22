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
using System.Threading;
using System.Threading.Tasks;
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
        private readonly List<MeasurementProcessor> measurementProcessors = new List<MeasurementProcessor>();
        private readonly List<MetricProcessor> metricProcessors = new List<MetricProcessor>();

        internal MeterProviderSdk(
            Resource resource,
            IEnumerable<string> meterSources,
            List<MeterProviderBuilderSdk.InstrumentationFactory> instrumentationFactories,
            MeasurementProcessor[] measurementProcessors,
            MetricProcessor[] metricProcessors)
        {
            this.Resource = resource;

            // TODO: Replace with single CompositeProcessor.
            this.measurementProcessors.AddRange(measurementProcessors);
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
                        var instrumentState = new InstrumentState(this, instrument);
                        listener.EnableMeasurementEvents(instrument, instrumentState);
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
            var instrumentState = state as InstrumentState;

            if (instrument == null || instrumentState == null)
            {
                // TODO: log
                return;
            }

            var measurementItem = new MeasurementItem(instrument, instrumentState);
            var tags = tagsRos;
            var val = value;

            // Run measurement Processors
            foreach (var processor in this.measurementProcessors)
            {
                processor.OnEnd(measurementItem, ref val, ref tags);
            }

            // TODO: Replace the following with a built-in MeasurementProcessor
            // that knows how to aggregate and produce Metrics.
            instrumentState.Update(val, tags);
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

            foreach (var processor in this.measurementProcessors)
            {
                processor.Dispose();
            }

            this.listener.Dispose();
        }

        private MetricItem Collect(bool isDelta)
        {
            lock (this.collectLock)
            {
                // Record all observable instruments
                this.listener.RecordObservableInstruments();
                var metricItem = new MetricItem();

                foreach (var kv in this.AggregatorStores)
                {
                    var metrics = kv.Key.Collect(isDelta);
                    metricItem.Metrics.AddRange(metrics);
                }

                return metricItem;
            }
        }
    }
}
