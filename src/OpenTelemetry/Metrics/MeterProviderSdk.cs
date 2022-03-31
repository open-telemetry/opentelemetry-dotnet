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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    internal sealed class MeterProviderSdk : MeterProvider
    {
        internal int ShutdownCount;
        private readonly List<object> instrumentations = new();
        private readonly List<Func<Instrument, MetricStreamConfiguration>> viewConfigs;
        private readonly object collectLock = new();
        private readonly MeterListener listener;
        private readonly MetricReader reader;
        private readonly CompositeMetricReader compositeMetricReader;
        private bool disposed;

        internal MeterProviderSdk(
            Resource resource,
            IEnumerable<string> meterSources,
            List<MeterProviderBuilderBase.InstrumentationFactory> instrumentationFactories,
            List<Func<Instrument, MetricStreamConfiguration>> viewConfigs,
            int maxMetricStreams,
            int maxMetricPointsPerMetricStream,
            IEnumerable<MetricReader> readers)
        {
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent("Building MeterProvider.");

            StringBuilder exportersAdded = new StringBuilder();
            StringBuilder instrumentationFactoriesAdded = new StringBuilder();

            this.Resource = resource;
            this.viewConfigs = viewConfigs;

            foreach (var reader in readers)
            {
                Guard.ThrowIfNull(reader);

                reader.SetParentProvider(this);
                reader.SetMaxMetricStreams(maxMetricStreams);
                reader.SetMaxMetricPointsPerMetricStream(maxMetricPointsPerMetricStream);

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

                if (reader is PeriodicExportingMetricReader periodicExportingMetricReader)
                {
                    exportersAdded.Append(periodicExportingMetricReader.Exporter);
                    exportersAdded.Append(" (Paired with PeriodicExportingMetricReader exporting at ");
                    exportersAdded.Append(periodicExportingMetricReader.ExportIntervalMilliseconds);
                    exportersAdded.Append(" milliseconds intervals.)");
                    exportersAdded.Append(';');
                }
                else if (reader is BaseExportingMetricReader baseExportingMetricReader)
                {
                    exportersAdded.Append(baseExportingMetricReader.Exporter);
                    exportersAdded.Append(" (Paired with a MetricReader requiring manual trigger to export.)");
                    exportersAdded.Append(';');
                }
            }

            if (exportersAdded.Length != 0)
            {
                exportersAdded.Remove(exportersAdded.Length - 1, 1);
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Exporters added = \"{exportersAdded}\".");
            }

            this.compositeMetricReader = this.reader as CompositeMetricReader;

            if (instrumentationFactories.Any())
            {
                foreach (var instrumentationFactory in instrumentationFactories)
                {
                    this.instrumentations.Add(instrumentationFactory.Factory());
                    instrumentationFactoriesAdded.Append(instrumentationFactory.Name);
                    instrumentationFactoriesAdded.Append(';');
                }
            }

            if (instrumentationFactoriesAdded.Length != 0)
            {
                instrumentationFactoriesAdded.Remove(instrumentationFactoriesAdded.Length - 1, 1);
                OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Instrumentations added = \"{instrumentationFactoriesAdded}\".");
            }

            // Setup Listener
            Func<Instrument, bool> shouldListenTo = instrument => false;
            if (meterSources.Any(s => WildcardHelper.ContainsWildcard(s)))
            {
                var regex = WildcardHelper.GetWildcardRegex(meterSources);
                shouldListenTo = instrument => regex.IsMatch(instrument.Meter.Name);
            }
            else if (meterSources.Any())
            {
                var meterSourcesToSubscribe = new HashSet<string>(meterSources, StringComparer.OrdinalIgnoreCase);
                shouldListenTo = instrument => meterSourcesToSubscribe.Contains(instrument.Meter.Name);
            }

            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Listening to following meters = \"{string.Join(";", meterSources)}\".");

            this.listener = new MeterListener();
            var viewConfigCount = this.viewConfigs.Count;

            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Number of views configured = {viewConfigCount}.");

            // We expect that all the readers to be added are provided before MeterProviderSdk is built.
            // If there are no readers added, we do not enable measurements for the instruments.
            if (viewConfigCount > 0)
            {
                this.listener.InstrumentPublished = (instrument, listener) =>
                {
                    if (!shouldListenTo(instrument))
                    {
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(instrument.Name, instrument.Meter.Name, "Instrument belongs to a Meter not subscribed by the provider.", "Use AddMeter to add the Meter to the provider.");
                        return;
                    }

                    try
                    {
                        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Started publishing Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\".");

                        // Creating list with initial capacity as the maximum
                        // possible size, to avoid any array resize/copy internally.
                        // There may be excess space wasted, but it'll eligible for
                        // GC right after this method.
                        var metricStreamConfigs = new List<MetricStreamConfiguration>(viewConfigCount);
                        foreach (var viewConfig in this.viewConfigs)
                        {
                            MetricStreamConfiguration metricStreamConfig = null;

                            try
                            {
                                metricStreamConfig = viewConfig(instrument);
                            }
                            catch (Exception ex)
                            {
                                // TODO: This allocates the string even if none listening,
                                // could be improved with a separate dedicated method.
                                OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(instrument.Name, instrument.Meter.Name, "Applying View Configuration failed with error:" + ex.Message, "Fix the view configuration.");
                                return;
                            }

                            if (metricStreamConfig != null)
                            {
                                metricStreamConfigs.Add(metricStreamConfig);
                            }
                        }

                        if (metricStreamConfigs.Count == 0)
                        {
                            // No views matched. Add null
                            // which will apply defaults.
                            // Users can turn off this default
                            // by adding a view like below as the last view.
                            // .AddView(instrumentName: "*", MetricStreamConfiguration.Drop)
                            metricStreamConfigs.Add(null);
                        }

                        if (this.reader != null)
                        {
                            if (this.compositeMetricReader == null)
                            {
                                var metrics = this.reader.AddMetricsListWithViews(instrument, metricStreamConfigs);
                                if (metrics.Count > 0)
                                {
                                    listener.EnableMeasurementEvents(instrument, metrics);
                                }
                            }
                            else
                            {
                                var metricsSuperList = this.compositeMetricReader.AddMetricsSuperListWithViews(instrument, metricStreamConfigs);
                                if (metricsSuperList.Any(metrics => metrics.Count > 0))
                                {
                                    listener.EnableMeasurementEvents(instrument, metricsSuperList);
                                }
                            }
                        }

                        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Completed publishing Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\".");
                    }
                    catch (Exception)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(instrument.Name, instrument.Meter.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    }
                };

                // Everything double
                this.listener.SetMeasurementEventCallback<double>(this.MeasurementRecordedDouble);
                this.listener.SetMeasurementEventCallback<float>((instrument, value, tags, state) => this.MeasurementRecordedDouble(instrument, value, tags, state));

                // Everything long
                this.listener.SetMeasurementEventCallback<long>(this.MeasurementRecordedLong);
                this.listener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => this.MeasurementRecordedLong(instrument, value, tags, state));
                this.listener.SetMeasurementEventCallback<short>((instrument, value, tags, state) => this.MeasurementRecordedLong(instrument, value, tags, state));
                this.listener.SetMeasurementEventCallback<byte>((instrument, value, tags, state) => this.MeasurementRecordedLong(instrument, value, tags, state));

                this.listener.MeasurementsCompleted = (instrument, state) => this.MeasurementsCompleted(instrument, state);
            }
            else
            {
                this.listener.InstrumentPublished = (instrument, listener) =>
                {
                    if (!shouldListenTo(instrument))
                    {
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(instrument.Name, instrument.Meter.Name, "Instrument belongs to a Meter not subscribed by the provider.", "Use AddMeter to add the Meter to the provider.");
                        return;
                    }

                    try
                    {
                        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Started publishing Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\".");

                        if (!MeterProviderBuilderSdk.IsValidInstrumentName(instrument.Name))
                        {
                            OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                                instrument.Name,
                                instrument.Meter.Name,
                                "Instrument name is invalid.",
                                "The name must comply with the OpenTelemetry specification");

                            return;
                        }

                        if (this.reader != null)
                        {
                            if (this.compositeMetricReader == null)
                            {
                                var metric = this.reader.AddMetricWithNoViews(instrument);
                                if (metric != null)
                                {
                                    listener.EnableMeasurementEvents(instrument, metric);
                                }
                            }
                            else
                            {
                                var metrics = this.compositeMetricReader.AddMetricsWithNoViews(instrument);
                                if (metrics.Any(metric => metric != null))
                                {
                                    listener.EnableMeasurementEvents(instrument, metrics);
                                }
                            }
                        }

                        OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"Completed publishing Instrument = \"{instrument.Name}\" of Meter = \"{instrument.Meter.Name}\".");
                    }
                    catch (Exception)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(instrument.Name, instrument.Meter.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    }
                };

                // Everything double
                this.listener.SetMeasurementEventCallback<double>(this.MeasurementRecordedDoubleSingleStream);
                this.listener.SetMeasurementEventCallback<float>((instrument, value, tags, state) => this.MeasurementRecordedDoubleSingleStream(instrument, value, tags, state));

                // Everything long
                this.listener.SetMeasurementEventCallback<long>(this.MeasurementRecordedLongSingleStream);
                this.listener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => this.MeasurementRecordedLongSingleStream(instrument, value, tags, state));
                this.listener.SetMeasurementEventCallback<short>((instrument, value, tags, state) => this.MeasurementRecordedLongSingleStream(instrument, value, tags, state));
                this.listener.SetMeasurementEventCallback<byte>((instrument, value, tags, state) => this.MeasurementRecordedLongSingleStream(instrument, value, tags, state));

                this.listener.MeasurementsCompleted = (instrument, state) => this.MeasurementsCompletedSingleStream(instrument, state);
            }

            this.listener.Start();

            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent("MeterProvider built successfully.");
        }

        internal Resource Resource { get; }

        internal List<object> Instrumentations => this.instrumentations;

        internal MetricReader Reader => this.reader;

        internal void MeasurementsCompletedSingleStream(Instrument instrument, object state)
        {
            Debug.Assert(instrument != null, "instrument must be non-null.");

            if (this.compositeMetricReader == null)
            {
                if (state is not Metric metric)
                {
                    // TODO: log
                    return;
                }

                this.reader.CompleteSingleStreamMeasurement(metric);
            }
            else
            {
                if (state is not List<Metric> metrics)
                {
                    // TODO: log
                    return;
                }

                this.compositeMetricReader.CompleteSingleStreamMeasurements(metrics);
            }
        }

        internal void MeasurementsCompleted(Instrument instrument, object state)
        {
            Debug.Assert(instrument != null, "instrument must be non-null.");

            if (this.compositeMetricReader == null)
            {
                if (state is not List<Metric> metrics)
                {
                    // TODO: log
                    return;
                }

                this.reader.CompleteMeasurement(metrics);
            }
            else
            {
                if (state is not List<List<Metric>> metricsSuperList)
                {
                    // TODO: log
                    return;
                }

                this.compositeMetricReader.CompleteMesaurements(metricsSuperList);
            }
        }

        internal void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            Debug.Assert(instrument != null, "instrument must be non-null.");

            if (this.compositeMetricReader == null)
            {
                if (state is not List<Metric> metrics)
                {
                    OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    return;
                }

                this.reader.RecordDoubleMeasurement(metrics, value, tagsRos);
            }
            else
            {
                if (state is not List<List<Metric>> metricsSuperList)
                {
                    OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    return;
                }

                this.compositeMetricReader.RecordDoubleMeasurements(metricsSuperList, value, tagsRos);
            }
        }

        internal void MeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            Debug.Assert(instrument != null, "instrument must be non-null.");

            if (this.compositeMetricReader == null)
            {
                if (state is not List<Metric> metrics)
                {
                    OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    return;
                }

                this.reader.RecordLongMeasurement(metrics, value, tagsRos);
            }
            else
            {
                if (state is not List<List<Metric>> metricsSuperList)
                {
                    OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    return;
                }

                this.compositeMetricReader.RecordLongMeasurements(metricsSuperList, value, tagsRos);
            }
        }

        internal void MeasurementRecordedLongSingleStream(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            Debug.Assert(instrument != null, "instrument must be non-null.");

            if (this.compositeMetricReader == null)
            {
                if (state is not Metric metric)
                {
                    OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    return;
                }

                this.reader.RecordSingleStreamLongMeasurement(metric, value, tagsRos);
            }
            else
            {
                if (state is not List<Metric> metrics)
                {
                    OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    return;
                }

                this.compositeMetricReader.RecordSingleStreamLongMeasurements(metrics, value, tagsRos);
            }
        }

        internal void MeasurementRecordedDoubleSingleStream(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            Debug.Assert(instrument != null, "instrument must be non-null.");

            if (this.compositeMetricReader == null)
            {
                if (state is not Metric metric)
                {
                    OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    return;
                }

                this.reader.RecordSingleStreamDoubleMeasurement(metric, value, tagsRos);
            }
            else
            {
                if (state is not List<Metric> metrics)
                {
                    OpenTelemetrySdkEventSource.Log.MeasurementDropped(instrument.Name, "SDK internal error occurred.", "Contact SDK owners.");
                    return;
                }

                this.compositeMetricReader.RecordSingleStreamDoubleMeasurements(metrics, value, tagsRos);
            }
        }

        internal void CollectObservableInstruments()
        {
            lock (this.collectLock)
            {
                // Record all observable instruments
                try
                {
                    this.listener.RecordObservableInstruments();
                }
                catch (Exception exception)
                {
                    // TODO:
                    // It doesn't looks like we can find which instrument callback
                    // threw.
                    OpenTelemetrySdkEventSource.Log.MetricObserverCallbackException(exception);
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
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.OnForceFlush)} called with {nameof(timeoutMilliseconds)} = {timeoutMilliseconds}.");
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
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.OnShutdown)} called with {nameof(timeoutMilliseconds)} = {timeoutMilliseconds}.");
            return this.reader?.Shutdown(timeoutMilliseconds) ?? true;
        }

        protected override void Dispose(bool disposing)
        {
            OpenTelemetrySdkEventSource.Log.MeterProviderSdkEvent($"{nameof(MeterProviderSdk)}.{nameof(this.Dispose)} started.");
            if (!this.disposed)
            {
                if (disposing)
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
                    this.compositeMetricReader?.Dispose();

                    this.listener.Dispose();
                }

                this.disposed = true;
                OpenTelemetrySdkEventSource.Log.ProviderDisposed(nameof(MeterProvider));
            }

            base.Dispose(disposing);
        }
    }
}
