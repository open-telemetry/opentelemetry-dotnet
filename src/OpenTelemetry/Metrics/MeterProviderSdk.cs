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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    internal sealed class MeterProviderSdk : MeterProvider
    {
        internal const int MaxMetrics = 1000;
        internal int ShutdownCount;
        private readonly Metric[] metrics;
        private readonly List<object> instrumentations = new List<object>();
        private readonly object collectLock = new object();
        private readonly MeterListener listener;
        private readonly MetricReader reader;
        private int metricIndex = -1;

        internal MeterProviderSdk(
            Resource resource,
            IEnumerable<string> meterSources,
            List<MeterProviderBuilderSdk.InstrumentationFactory> instrumentationFactories,
            IEnumerable<MetricReader> readers)
        {
            this.Resource = resource;
            this.metrics = new Metric[MaxMetrics];

            AggregationTemporality temporality = AggregationTemporality.Cumulative;

            foreach (var reader in readers)
            {
                if (reader == null)
                {
                    throw new ArgumentException("A null value was found.", nameof(readers));
                }

                reader.SetParentProvider(this);

                // TODO: Actually support multiple readers.
                // Currently the last reader's temporality wins.
                temporality = reader.PreferredAggregationTemporality;

                if (this.reader == null)
                {
                    this.reader = reader;
                }
                else if (this.reader is CompositeMetricReader compositeReader)
                {
                    compositeReader.AddReader(reader);
                }
                else
                {
                    this.reader = new CompositeMetricReader(new[] { this.reader, reader });
                }
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
                        var index = Interlocked.Increment(ref this.metricIndex);
                        if (index >= MaxMetrics)
                        {
                            // Log that all measurements are dropped from this instrument.
                        }
                        else
                        {
                            var metric = new Metric(instrument, temporality);
                            this.metrics[index] = metric;
                            listener.EnableMeasurementEvents(instrument, metric);
                        }
                    }
                },
                MeasurementsCompleted = (instrument, state) => this.MeasurementsCompleted(instrument, state),
            };

            // Everything double
            this.listener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => this.MeasurementRecordedDouble(instrument, value, tags, state));
            this.listener.SetMeasurementEventCallback<float>((instrument, value, tags, state) => this.MeasurementRecordedDouble(instrument, value, tags, state));

            // Everything long
            this.listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => this.MeasurementRecordedLong(instrument, value, tags, state));
            this.listener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => this.MeasurementRecordedLong(instrument, value, tags, state));
            this.listener.SetMeasurementEventCallback<short>((instrument, value, tags, state) => this.MeasurementRecordedLong(instrument, value, tags, state));
            this.listener.SetMeasurementEventCallback<byte>((instrument, value, tags, state) => this.MeasurementRecordedLong(instrument, value, tags, state));

            this.listener.Start();
        }

        internal Resource Resource { get; }

        internal void MeasurementsCompleted(Instrument instrument, object state)
        {
            Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} completed.");
        }

        internal void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            var metric = state as Metric;

            if (instrument == null || metric == null)
            {
                // TODO: log
                return;
            }

            metric.UpdateDouble(value, tagsRos);
        }

        internal void MeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            var metric = state as Metric;

            if (instrument == null || metric == null)
            {
                // TODO: log
                return;
            }

            metric.UpdateLong(value, tagsRos);
        }

        internal Batch<Metric> Collect()
        {
            lock (this.collectLock)
            {
                try
                {
                    // Record all observable instruments
                    this.listener.RecordObservableInstruments();
                    var indexSnapShot = Math.Min(this.metricIndex, MaxMetrics - 1);
                    for (int i = 0; i < indexSnapShot + 1; i++)
                    {
                        this.metrics[i].SnapShot();
                    }

                    return new Batch<Metric>(this.metrics, indexSnapShot + 1);
                }
                catch (Exception)
                {
                    // TODO: Log
                    return default;
                }
            }
        }

        /// <summary>
        /// Called by <c>ForceFlush</c>. This function should block the current
        /// thread until flush completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when flush succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This function is called synchronously on the thread which made the
        /// first call to <c>ForceFlush</c>. This function should not throw
        /// exceptions.
        /// </remarks>
        internal bool OnForceFlush(int timeoutMilliseconds)
        {
            return this.reader?.Collect(timeoutMilliseconds) ?? true;
        }

        /// <summary>
        /// Called by <c>Shutdown</c>. This function should block the current
        /// thread until shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This function is called synchronously on the thread which made the
        /// first call to <c>Shutdown</c>. This function should not throw
        /// exceptions.
        /// </remarks>
        internal bool OnShutdown(int timeoutMilliseconds)
        {
            return this.reader?.Shutdown(timeoutMilliseconds) ?? true;
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

            // Wait for up to 5 seconds grace period
            this.reader?.Shutdown(5000);
            this.reader?.Dispose();

            this.listener.Dispose();
        }
    }
}
