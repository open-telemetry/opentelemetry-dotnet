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
using System.Text.RegularExpressions;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    internal sealed class MeterProviderSdk : MeterProvider
    {
        internal const int MaxMetrics = 1000;
        internal int ShutdownCount;
        private readonly Metric[] metrics;
        private readonly Metric[] metricsCurrentBatch;
        private readonly List<object> instrumentations = new List<object>();
        private readonly List<Func<Instrument, MetricStreamConfiguration>> viewConfigs;
        private readonly object collectLock = new object();
        private readonly object instrumentCreationLock = new object();
        private readonly HashSet<string> metricStreamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly MeterListener listener;
        private readonly MetricReader reader;
        private int metricIndex = -1;
        private bool disposed;

        internal MeterProviderSdk(
            Resource resource,
            IEnumerable<string> meterSources,
            List<MeterProviderBuilderBase.InstrumentationFactory> instrumentationFactories,
            List<Func<Instrument, MetricStreamConfiguration>> viewConfigs,
            IEnumerable<MetricReader> readers)
        {
            this.Resource = resource;
            this.viewConfigs = viewConfigs;
            this.metrics = new Metric[MaxMetrics];
            this.metricsCurrentBatch = new Metric[MaxMetrics];

            AggregationTemporality temporality = AggregationTemporality.Cumulative;

            foreach (var reader in readers)
            {
                Guard.Null(reader, nameof(reader));

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
            Func<Instrument, bool> shouldListenTo = instrument => false;
            if (meterSources.Any(s => s.Contains('*')))
            {
                var regex = GetWildcardRegex(meterSources);
                shouldListenTo = instrument => regex.IsMatch(instrument.Meter.Name);
            }
            else if (meterSources.Any())
            {
                var meterSourcesToSubscribe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var meterSource in meterSources)
                {
                    meterSourcesToSubscribe.Add(meterSource);
                }

                shouldListenTo = instrument => meterSourcesToSubscribe.Contains(instrument.Meter.Name);
            }

            this.listener = new MeterListener();
            var viewConfigCount = this.viewConfigs.Count;
            if (viewConfigCount > 0)
            {
                this.listener.InstrumentPublished = (instrument, listener) =>
                {
                    if (!shouldListenTo(instrument))
                    {
                        OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(instrument.Name, instrument.Meter.Name, "Instrument belongs to a Meter not subscribed by the provider.", "Use AddMeter to add the Meter to the provider.");
                        return;
                    }

                    // Creating list with initial capacity as the maximum
                    // possible size, to avoid any array resize/copy internally.
                    // There may be excess space wasted, but it'll eligible for
                    // GC right after this method.
                    var metricStreamConfigs = new List<MetricStreamConfiguration>(viewConfigCount);
                    foreach (var viewConfig in this.viewConfigs)
                    {
                        var metricStreamConfig = viewConfig(instrument);
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

                    var maxCountMetricsToBeCreated = metricStreamConfigs.Count;

                    // Create list with initial capacity as the max metric count.
                    // Due to duplicate/max limit, we may not end up using them
                    // all, and that memory is wasted until Meter disposed.
                    // TODO: Revisit to see if we need to do metrics.TrimExcess()
                    var metrics = new List<Metric>(maxCountMetricsToBeCreated);
                    lock (this.instrumentCreationLock)
                    {
                        for (int i = 0; i < maxCountMetricsToBeCreated; i++)
                        {
                            var metricStreamConfig = metricStreamConfigs[i];
                            var metricStreamName = metricStreamConfig?.Name ?? instrument.Name;

                            if (!MeterProviderBuilderSdk.IsValidInstrumentName(metricStreamName))
                            {
                                OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                                    metricStreamName,
                                    instrument.Meter.Name,
                                    "Metric name is invalid.",
                                    "The name must comply with the OpenTelemetry specification.");

                                continue;
                            }

                            if (this.metricStreamNames.Contains(metricStreamName))
                            {
                                // TODO: Log that instrument is ignored
                                // as the resulting Metric name is conflicting
                                // with existing name.
                                continue;
                            }

                            if (metricStreamConfig?.Aggregation == Aggregation.Drop)
                            {
                                // TODO: Log that instrument is ignored
                                // as user explicitly asked to drop it
                                // with View.
                                continue;
                            }

                            var index = ++this.metricIndex;
                            if (index >= MaxMetrics)
                            {
                                // TODO: Log that instrument is ignored
                                // as max number of Metrics have reached.
                            }
                            else
                            {
                                Metric metric;
                                var metricDescription = metricStreamConfig?.Description ?? instrument.Description;
                                string[] tagKeysInteresting = metricStreamConfig?.TagKeys;
                                double[] histogramBucketBounds = (metricStreamConfig is HistogramConfiguration histogramConfig
                                    && histogramConfig.BucketBounds != null) ? histogramConfig.BucketBounds : null;
                                metric = new Metric(instrument, temporality, metricStreamName, metricDescription, histogramBucketBounds, tagKeysInteresting);

                                this.metrics[index] = metric;
                                metrics.Add(metric);
                                this.metricStreamNames.Add(metricStreamName);
                            }
                        }

                        if (metrics.Count > 0)
                        {
                            listener.EnableMeasurementEvents(instrument, metrics);
                        }
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
                        if (!MeterProviderBuilderSdk.IsValidInstrumentName(instrument.Name))
                        {
                            OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(
                                instrument.Name,
                                instrument.Meter.Name,
                                "Instrument name is invalid.",
                                "The name must comply with the OpenTelemetry specification");

                            return;
                        }

                        var metricName = instrument.Name;
                        Metric metric = null;
                        lock (this.instrumentCreationLock)
                        {
                            if (this.metricStreamNames.Contains(metricName))
                            {
                                OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricName, instrument.Meter.Name, "Metric name conflicting with existing name.", "Either change the name of the instrument or change name using View.");
                                return;
                            }

                            var index = ++this.metricIndex;
                            if (index >= MaxMetrics)
                            {
                                OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricName, instrument.Meter.Name, "Maximum allowed Metrics for the provider exceeded.", "Use views to drop unused instruments. Or configure Provider to allow higher limit.");
                                return;
                            }
                            else
                            {
                                metric = new Metric(instrument, temporality, metricName, instrument.Description);
                                this.metrics[index] = metric;
                                this.metricStreamNames.Add(metricName);
                            }
                        }

                        listener.EnableMeasurementEvents(instrument, metric);
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

            static Regex GetWildcardRegex(IEnumerable<string> collection)
            {
                var pattern = '^' + string.Join("|", from name in collection select "(?:" + Regex.Escape(name).Replace("\\*", ".*") + ')') + '$';
                return new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
        }

        internal Resource Resource { get; }

        internal List<object> Instrumentations => this.instrumentations;

        internal MetricReader Reader => this.reader;

        internal void MeasurementsCompletedSingleStream(Instrument instrument, object state)
        {
            var metric = state as Metric;
            if (metric == null)
            {
                // TODO: log
                return;
            }

            metric.InstrumentDisposed = true;
        }

        internal void MeasurementsCompleted(Instrument instrument, object state)
        {
            var metrics = state as List<Metric>;
            if (metrics == null)
            {
                // TODO: log
                return;
            }

            foreach (var metric in metrics)
            {
                metric.InstrumentDisposed = true;
            }
        }

        internal void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            var metrics = state as List<Metric>;

            Debug.Assert(instrument != null, "instrument must be non-null.");
            if (metrics == null)
            {
                // TODO: log
                return;
            }

            if (metrics.Count == 1)
            {
                // special casing the common path
                // as this is faster than the
                // foreach, when count is 1.
                metrics[0].UpdateDouble(value, tagsRos);
            }
            else
            {
                foreach (var metric in metrics)
                {
                    metric.UpdateDouble(value, tagsRos);
                }
            }
        }

        internal void MeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            var metrics = state as List<Metric>;

            Debug.Assert(instrument != null, "instrument must be non-null.");
            if (metrics == null)
            {
                // TODO: log
                return;
            }

            if (metrics.Count == 1)
            {
                // special casing the common path
                // as this is faster than the
                // foreach, when count is 1.
                metrics[0].UpdateLong(value, tagsRos);
            }
            else
            {
                foreach (var metric in metrics)
                {
                    metric.UpdateLong(value, tagsRos);
                }
            }
        }

        internal void MeasurementRecordedLongSingleStream(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            var metric = state as Metric;

            Debug.Assert(instrument != null, "instrument must be non-null.");
            if (metric == null)
            {
                // TODO: log
                return;
            }

            metric.UpdateLong(value, tagsRos);
        }

        internal void MeasurementRecordedDoubleSingleStream(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            var metric = state as Metric;

            Debug.Assert(instrument != null, "instrument must be non-null.");
            if (metric == null)
            {
                // TODO: log
                return;
            }

            metric.UpdateDouble(value, tagsRos);
        }

        internal Batch<Metric> Collect()
        {
            lock (this.collectLock)
            {
                try
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

                    var indexSnapShot = Math.Min(this.metricIndex, MaxMetrics - 1);
                    var target = indexSnapShot + 1;
                    int metricCountCurrentBatch = 0;
                    for (int i = 0; i < target; i++)
                    {
                        var metric = this.metrics[i];
                        if (metric != null)
                        {
                            if (metric.InstrumentDisposed)
                            {
                                metric.SnapShot();
                                this.metrics[i] = null;
                            }
                            else
                            {
                                metric.SnapShot();
                            }

                            this.metricsCurrentBatch[metricCountCurrentBatch++] = metric;
                        }
                    }

                    return (metricCountCurrentBatch > 0) ? new Batch<Metric>(this.metricsCurrentBatch, metricCountCurrentBatch) : default;
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

                    this.listener.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
