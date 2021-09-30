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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    internal sealed class MeterProviderSdk : MeterProvider
    {
        internal const int MaxMetrics = 1000;
        internal int ShutdownCount;
        private readonly Metric[] metrics;
        private readonly List<object> instrumentations = new List<object>();
        private readonly List<Func<Instrument, MetricStreamConfiguration>> viewConfigs;
        private readonly object collectLock = new object();
        private readonly object instrumentCreationLock = new object();
        private readonly Dictionary<string, bool> metricStreamNames = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly MeterListener listener;
        private readonly MetricReader reader;
        private int metricIndex = -1;

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

            this.listener = new MeterListener();
            var viewConfigCount = this.viewConfigs.Count;
            if (viewConfigCount > 0)
            {
                this.listener.InstrumentPublished = (instrument, listener) =>
                {
                    if (meterSourcesToSubscribe.ContainsKey(instrument.Meter.Name))
                    {
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
                            // .AddView(instrumentName: "*", new MetricStreamConfiguration() { Aggregation = Aggregation.Drop })
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
                                if (this.metricStreamNames.ContainsKey(metricStreamName))
                                {
                                    // TODO: Log that instrument is ignored
                                    // as the resulting Metric name is conflicting
                                    // with existing name.
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
                                    if (metricStreamConfig is HistogramConfiguration histogramConfig
                                        && histogramConfig.BucketBounds != null)
                                    {
                                        metric = new Metric(instrument, temporality, histogramConfig.BucketBounds, metricStreamName);
                                    }
                                    else
                                    {
                                        metric = new Metric(instrument, temporality, metricStreamName);
                                    }

                                    this.metrics[index] = metric;
                                    metrics.Add(metric);
                                    this.metricStreamNames.Add(metricStreamName, true);
                                }
                            }
                        }

                        if (metrics.Count > 0)
                        {
                            listener.EnableMeasurementEvents(instrument, metrics);
                        }
                    }
                };
            }
            else
            {
                this.listener.InstrumentPublished = (instrument, listener) =>
                {
                    if (meterSourcesToSubscribe.ContainsKey(instrument.Meter.Name))
                    {
                        var metricName = instrument.Name;
                        List<Metric> metrics = null;
                        lock (this.instrumentCreationLock)
                        {
                            if (this.metricStreamNames.ContainsKey(metricName))
                            {
                                // TODO: Log that instrument is ignored
                                // as the resulting Metric name is conflicting
                                // with existing name.
                                return;
                            }

                            var index = ++this.metricIndex;
                            if (index >= MaxMetrics)
                            {
                                // TODO: Log that instrument is ignored
                                // as max number of Metrics have reached.
                                return;
                            }
                            else
                            {
                                metrics = new List<Metric>(1);
                                var metric = new Metric(instrument, temporality);
                                this.metrics[index] = metric;
                                metrics.Add(metric);
                                this.metricStreamNames.Add(metricName, true);
                            }
                        }

                        listener.EnableMeasurementEvents(instrument, metrics);
                    }
                };
            }

            this.listener.MeasurementsCompleted = (instrument, state) => this.MeasurementsCompleted(instrument, state);

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

        internal List<object> Instrumentations => this.instrumentations;

        internal MetricReader Reader => this.reader;

        internal void MeasurementsCompleted(Instrument instrument, object state)
        {
            Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} completed.");
        }

        internal void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            // TODO: Benchmark and see if it makes
            // sense to use a different state
            // when there are no views registered.
            // In that case, storing Metric as state
            // might be faster than storing List<Metric>
            // of size one as state.
            var metrics = state as List<Metric>;

            Debug.Assert(instrument != null, "instrument must be non-null.");
            if (metrics == null)
            {
                // TODO: log
                return;
            }

            foreach (var metric in metrics)
            {
                metric.UpdateDouble(value, tagsRos);
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

            foreach (var metric in metrics)
            {
                metric.UpdateLong(value, tagsRos);
            }
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
                    var target = indexSnapShot + 1;
                    for (int i = 0; i < target; i++)
                    {
                        this.metrics[i].SnapShot();
                    }

                    return (target > 0) ? new Batch<Metric>(this.metrics, target) : default;
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
