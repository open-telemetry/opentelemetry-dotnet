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
        private ConcurrentDictionary<Meter, int> meters;
        private MeterListener listener;
        private CancellationTokenSource cts;
        private Task observerTask;

        internal MeterProviderSdk(IEnumerable<string> meterSources, int observationPeriodMilliseconds)
        {
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

            this.cts = new CancellationTokenSource();

            var token = this.cts.Token;
            this.observerTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(observationPeriodMilliseconds, token);
                    }
                    catch (TaskCanceledException)
                    {
                    }

                    this.listener.RecordObservableInstruments();
                }
            });
        }

        internal void MeasurementsCompleted(Instrument instrument, object? state)
        {
            Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} completed.");
        }

        internal void MeasurementRecorded<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> attribs, object? state)
        {
            Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} recorded {value}.");
        }

        protected override void Dispose(bool disposing)
        {
            this.cts.Cancel();
            this.observerTask.Wait();
        }
    }
}
