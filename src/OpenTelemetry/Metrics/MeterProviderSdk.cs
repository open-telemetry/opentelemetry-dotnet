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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics
{
    public class MeterProviderSdk
        : MeterProvider
    {
        internal readonly ConcurrentDictionary<AggregatorStore, bool> AggregatorStores = new ConcurrentDictionary<AggregatorStore, bool>();

        private static int lastTick = -1;
        private static DateTimeOffset lastTimestamp = DateTimeOffset.MinValue;

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly List<Task> collectorTasks = new List<Task>();
        private readonly MeterListener listener;

        private readonly object lockInstrumentStates = new object();
        private readonly Dictionary<Instrument, InstrumentState> instrumentStates = new Dictionary<Instrument, InstrumentState>();

        internal MeterProviderSdk(
            IEnumerable<string> meterSources,
            MeasurementProcessor[] measurementProcessors,
            KeyValuePair<MetricProcessor, int>[] metricExportProcessors)
        {
            // Setup our Processors

            this.MeasurementProcessors.AddRange(measurementProcessors);

            this.ExportProcessors.AddRange(metricExportProcessors);

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
                    Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} published.");
                    if (meterSourcesToSubscribe.ContainsKey(instrument.Meter.Name))
                    {
                        var instrumentState = new InstrumentState(this, instrument);

                        lock (this.lockInstrumentStates)
                        {
                            this.instrumentStates.Add(instrument, instrumentState);
                        }

                        listener.EnableMeasurementEvents(instrument, instrumentState);
                    }
                },
                MeasurementsCompleted = (instrument, state) => this.MeasurementsCompleted(instrument, state),
            };

            this.listener.SetMeasurementEventCallback<double>((i, m, l, c) => this.MeasurementRecorded(i, m, l, c));
            this.listener.SetMeasurementEventCallback<float>((i, m, l, c) => this.MeasurementRecorded(i, m, l, c));
            this.listener.SetMeasurementEventCallback<long>((i, m, l, c) => this.MeasurementRecorded(i, m, l, c));
            this.listener.SetMeasurementEventCallback<int>((i, m, l, c) => this.MeasurementRecorded(i, m, l, c));
            this.listener.SetMeasurementEventCallback<short>((i, m, l, c) => this.MeasurementRecorded(i, m, l, c));
            this.listener.SetMeasurementEventCallback<byte>((i, m, l, c) => this.MeasurementRecorded(i, m, l, c));

            this.listener.Start();

            // Start our long running Task

            var token = this.cts.Token;

            // Group Export processors by their collectionPeriod.
            var groups = this.ExportProcessors.GroupBy(k => k.Value, v => v.Key);
            foreach (var group in groups)
            {
                this.collectorTasks.Add(Task.Run(async () => await this.CollectorTask(token, group.Key, group.ToArray())));
            }
        }

        internal List<MeasurementProcessor> MeasurementProcessors { get; } = new List<MeasurementProcessor>();

        internal List<MetricProcessor> MetricProcessors { get; } = new List<MetricProcessor>();

        internal List<KeyValuePair<MetricProcessor, int>> ExportProcessors { get; } = new List<KeyValuePair<MetricProcessor, int>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static DateTimeOffset GetDateTimeOffset()
        {
            int tick = Environment.TickCount;
            if (tick == MeterProviderSdk.lastTick)
            {
                return MeterProviderSdk.lastTimestamp;
            }

            var dt = DateTimeOffset.UtcNow;
            MeterProviderSdk.lastTimestamp = dt;
            MeterProviderSdk.lastTick = tick;

            return dt;
        }

        internal void MeasurementsCompleted(Instrument instrument, object state)
        {
            Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} completed.");
        }

        internal void MeasurementRecorded<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
            where T : struct
        {
            // Get Instrument State

            if (!(state is InstrumentState instrumentState))
            {
                lock (this.lockInstrumentStates)
                {
                    if (!this.instrumentStates.TryGetValue(instrument, out instrumentState))
                    {
                        instrumentState = new InstrumentState(this, instrument);
                        this.instrumentStates.Add(instrument, instrumentState);
                    }
                }
            }

            var measurementItem = new MeasurementItem(instrument, instrumentState);
            var dt = MeterProviderSdk.GetDateTimeOffset();
            var tags = tagsRos;
            var val = value;

            // Run Pre Aggregator Processors
            foreach (var processor in this.MeasurementProcessors)
            {
                processor.OnEnd(measurementItem, ref dt, ref val, ref tags);
            }

            instrumentState.Update(dt, val, tags);
        }

        protected override void Dispose(bool disposing)
        {
            this.listener.Dispose();

            this.cts.Cancel();

            foreach (var collectorTask in this.collectorTasks)
            {
                collectorTask.Wait();
            }
        }

        private async Task CollectorTask(CancellationToken token, int collectionPeriodMilliseconds, MetricProcessor[] processors)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(collectionPeriodMilliseconds, token);
                }
                catch (TaskCanceledException)
                {
                }

                this.Collect(collectionPeriodMilliseconds, processors);
            }
        }

        private void Collect(int collectionPeriodMilliseconds, MetricProcessor[] processors)
        {
            // Record all observable instruments
            this.listener.RecordObservableInstruments();

            var metricItem = new MetricItem();

            foreach (var kv in this.AggregatorStores)
            {
                var metrics = kv.Key.Collect(collectionPeriodMilliseconds);
                metricItem.Metrics.AddRange(metrics);
            }

            foreach (var processor in this.MetricProcessors)
            {
                processor.OnEnd(metricItem);
            }

            foreach (var processor in processors)
            {
                processor.OnEnd(metricItem);
            }
        }
    }
}
