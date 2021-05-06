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
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace OpenTelemetry.Metrics
{
    public class MeterProviderSdk
        : MeterProvider
    {
        private readonly CancellationTokenSource cts;
        private readonly Task observerTask;
        private readonly Task exporterTask;
        private readonly ConcurrentDictionary<Meter, int> meters;
        private readonly MeterListener listener;

        internal MeterProviderSdk(
            IEnumerable<string> meterSources,
            int observationPeriodMilliseconds,
            int exportPeriodMilliseconds,
            MeasurementProcessor[] measurementProcessors,
            ExportMetricProcessor[] exportMetricProcessors)
        {
            this.ObservationPeriodMilliseconds = observationPeriodMilliseconds;
            this.ExportPeriodMilliseconds = exportPeriodMilliseconds;

            // Setup our Processors

            this.MeasurementProcessors.AddRange(measurementProcessors);

            this.AggregatorProcessors.Add(new AggregatorProcessor());

            this.ExportProcessors.AddRange(exportMetricProcessors);

            // Setup Listener

            this.meters = new ConcurrentDictionary<Meter, int>();

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
                            listener.EnableMeasurementEvents(instrument, null);
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

            this.cts = new CancellationTokenSource();

            var token = this.cts.Token;
            this.observerTask = Task.Run(async () => await this.ObserverTask(token));
            this.exporterTask = Task.Run(async () => await this.ExporterTask(token));
        }

        public int ObservationPeriodMilliseconds { get; } = 1000;

        public int ExportPeriodMilliseconds { get; } = 1000;

        public List<MeasurementProcessor> MeasurementProcessors { get; } = new List<MeasurementProcessor>();

        public List<AggregatorProcessor> AggregatorProcessors { get; }  = new List<AggregatorProcessor>();

        public List<ExportMetricProcessor> ExportProcessors { get; } = new List<ExportMetricProcessor>();

        internal void MeasurementsCompleted(Instrument instrument, object? state)
        {
            Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} completed.");
        }

        internal void MeasurementRecorded<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
            where T : unmanaged
        {
            // Run Pre Aggregator Processors

            var measurmentContext = new MeasurementContext(instrument, new DataPoint<T>(value, tags));

            foreach (var processor in this.MeasurementProcessors)
            {
                processor.OnStart(measurmentContext);
            }

            foreach (var processor in this.MeasurementProcessors)
            {
                processor.OnEnd(measurmentContext);
            }

            // Run Aggregator Processors

            foreach (var processor in this.AggregatorProcessors)
            {
                processor.OnStart(measurmentContext);
                processor.OnEnd(measurmentContext);
            }
        }

        protected override void Dispose(bool disposing)
        {
            this.cts.Cancel();

            this.observerTask.Wait();

            this.exporterTask.Wait();

            this.Export();
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

        private async Task ExporterTask(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.ExportPeriodMilliseconds, token);
                }
                catch (TaskCanceledException)
                {
                }

                this.Export();
            }
        }

        private void Export()
        {
            var exportContext = new ExportMetricContext();

            foreach (var processor in this.AggregatorProcessors)
            {
                var export = processor.Collect();
                exportContext.Exports.Add(export);
            }

            foreach (var processor in this.ExportProcessors)
            {
                processor.OnStart(exportContext);
                processor.OnEnd(exportContext);
            }
        }
    }
}
