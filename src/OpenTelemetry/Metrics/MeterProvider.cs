// <copyright file="MeterProvider.cs" company="OpenTelemetry Authors">
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
    public class MeterProvider
        : IDisposable
    {
        private BuildOptions options;
        private ConcurrentDictionary<Meter, int> meters;
        private MeterListener listener;
        private CancellationTokenSource cts;
        private Task observerTask;

        internal MeterProvider(BuildOptions options)
        {
            this.options = options;

            this.meters = new ConcurrentDictionary<Meter, int>();

            this.listener = new MeterListener()
            {
                InstrumentPublished = (instrument, listener) => this.InstrumentPublished(instrument, listener),
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
                        await Task.Delay(this.options.ObservationPeriodMilliseconds, token);
                    }
                    catch (TaskCanceledException)
                    {
                    }

                    this.listener.RecordObservableInstruments();
                }
            });
        }

        public Meter GetMeter(string name, string version)
        {
            var meter = new Meter(name, version);
            this.meters.TryAdd(meter, 0);

            return meter;
        }

        public void Dispose()
        {
            this.cts.Cancel();
            this.observerTask.Wait();
        }

        internal void InstrumentPublished(Instrument instrument, MeterListener listener)
        {
            bool isInclude = false;

            if (this.options.IncludeMeters != null && this.options.IncludeMeters.Length > 0)
            {
                foreach (var meterFunc in this.options.IncludeMeters)
                {
                    if (meterFunc(instrument))
                    {
                        isInclude = true;
                        break;
                    }
                }
            }
            else
            {
                isInclude = this.meters.TryGetValue(instrument.Meter, out var _);
            }

            if (isInclude)
            {
                // Enable this Instrument if it should be included.
                listener.EnableMeasurementEvents(instrument, null);

                if (this.options.Verbose)
                {
                    Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} published.");
                }
            }
        }

        internal void MeasurementsCompleted(Instrument instrument, object? state)
        {
            if (this.options.Verbose)
            {
                Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} completed.");
            }
        }

        internal void MeasurementRecorded<T>(Instrument instrument, T value, ReadOnlySpan<KeyValuePair<string, object?>> attribs, object? state)
        {
            if (this.options.Verbose)
            {
                Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} recorded {value}.");
            }
        }

        internal class BuildOptions
        {
            public Func<Instrument, bool>[] IncludeMeters { get; set; } = new Func<Instrument, bool>[0];

            public bool Verbose { get; set; } = true;

            public int ObservationPeriodMilliseconds { get; set; } = 1000;
        }
    }
}
