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
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics
{
    public class MeterProviderSdk
        : MeterProvider
    {
        private static int lastTick = -1;
        private static DateTimeOffset lastTimestamp = DateTimeOffset.MinValue;

        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly Task observerTask;
        private readonly Task collectorTask;
        private readonly MeterListener listener;

        private readonly object lockInstrumentStates = new object();
        private readonly Dictionary<Instrument, InstrumentState> instrumentStates = new Dictionary<Instrument, InstrumentState>();

        internal MeterProviderSdk(
            IEnumerable<string> meterSources,
            int observationPeriodMilliseconds,
            int collectionPeriodMilliseconds,
            MeasurementProcessor[] measurementProcessors,
            MetricProcessor[] metricExportProcessors)
        {
            this.ObservationPeriodMilliseconds = observationPeriodMilliseconds;
            this.CollectionPeriodMilliseconds = collectionPeriodMilliseconds;

            // Setup our Processors

            this.MeasurementProcessors.AddRange(measurementProcessors);

            this.AggregateProcessors.Add(new AggregateProcessor());

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
            this.observerTask = Task.Run(async () => await this.ObserverTask(token));
            this.collectorTask = Task.Run(async () => await this.CollectorTask(token));
        }

        internal int ObservationPeriodMilliseconds { get; } = 1000;

        internal int CollectionPeriodMilliseconds { get; } = 1000;

        internal List<MeasurementProcessor> MeasurementProcessors { get; } = new List<MeasurementProcessor>();

        internal List<AggregateProcessor> AggregateProcessors { get; } = new List<AggregateProcessor>();

        internal List<MetricProcessor> MetricProcessors { get; } = new List<MetricProcessor>();

        internal List<MetricProcessor> ExportProcessors { get; } = new List<MetricProcessor>();

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

            // Run Aggregator Processors

            foreach (var processor in this.AggregateProcessors)
            {
                processor.OnEnd(measurementItem, ref dt, ref val, ref tags);
            }
        }

        protected override void Dispose(bool disposing)
        {
            this.listener.Dispose();

            this.cts.Cancel();

            this.observerTask.Wait();

            this.collectorTask.Wait();

            this.Collect();
        }

        private async Task ObserverTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.ObservationPeriodMilliseconds, token);
                }
                catch (TaskCanceledException)
                {
                }

                this.listener.RecordObservableInstruments();
            }
        }

        private async Task CollectorTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.CollectionPeriodMilliseconds, token);
                }
                catch (TaskCanceledException)
                {
                }

                this.Collect();
            }
        }

        private void Collect()
        {
            var metricItem = new MetricItem();

            foreach (var processor in this.AggregateProcessors)
            {
                var metrics = processor.Collect();
                metricItem.Metrics.AddRange(metrics);
            }

            foreach (var processor in this.MetricProcessors)
            {
                processor.OnEnd(metricItem);
            }

            foreach (var processor in this.ExportProcessors)
            {
                processor.OnEnd(metricItem);
            }
        }
    }
}
