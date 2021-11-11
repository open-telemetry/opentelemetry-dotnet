// <copyright file="MetricReader.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public abstract class MetricReader : IDisposable
    {
        internal const int MaxMetrics = 1000;
        private const AggregationTemporality CumulativeAndDelta = AggregationTemporality.Cumulative | AggregationTemporality.Delta;
        private readonly Metric[] metrics = new Metric[MaxMetrics];
        private readonly Metric[] metricsCurrentBatch = new Metric[MaxMetrics];
        private readonly HashSet<string> metricStreamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object instrumentCreationLock = new object();
        private readonly object newTaskLock = new object();
        private readonly object onCollectLock = new object();
        private readonly TaskCompletionSource<bool> shutdownTcs = new TaskCompletionSource<bool>();
        private int metricIndex = -1;
        private AggregationTemporality preferredAggregationTemporality = CumulativeAndDelta;
        private AggregationTemporality supportedAggregationTemporality = CumulativeAndDelta;
        private int shutdownCount;
        private TaskCompletionSource<bool> collectionTcs;

        public BaseProvider ParentProvider { get; private set; }

        public AggregationTemporality PreferredAggregationTemporality
        {
            get => this.preferredAggregationTemporality;
            set
            {
                ValidateAggregationTemporality(value, this.supportedAggregationTemporality);
                this.preferredAggregationTemporality = value;
            }
        }

        public AggregationTemporality SupportedAggregationTemporality
        {
            get => this.supportedAggregationTemporality;
            set
            {
                ValidateAggregationTemporality(this.preferredAggregationTemporality, value);
                this.supportedAggregationTemporality = value;
            }
        }

        /// <summary>
        /// Attempts to collect the metrics, blocks the current thread until
        /// metrics collection completed, shutdown signaled or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when metrics collection succeeded; otherwise,
        /// <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety. If multiple calls occurred
        /// simultaneously, they might get folded and result in less calls to
        /// the <c>OnCollect</c> callback for improved performance, as long as
        /// the semantic can be preserved.
        /// </remarks>
        public bool Collect(int timeoutMilliseconds = Timeout.Infinite)
        {
            Guard.InvalidTimeout(timeoutMilliseconds, nameof(timeoutMilliseconds));

            var shouldRunCollect = false;
            var tcs = this.collectionTcs;

            if (tcs == null)
            {
                lock (this.newTaskLock)
                {
                    tcs = this.collectionTcs;

                    if (tcs == null)
                    {
                        shouldRunCollect = true;
                        tcs = new TaskCompletionSource<bool>();
                        this.collectionTcs = tcs;
                    }
                }
            }

            if (!shouldRunCollect)
            {
                return Task.WaitAny(tcs.Task, this.shutdownTcs.Task, Task.Delay(timeoutMilliseconds)) == 0 ? tcs.Task.Result : false;
            }

            var result = false;
            try
            {
                lock (this.onCollectLock)
                {
                    this.collectionTcs = null;
                    result = this.OnCollect(timeoutMilliseconds);
                }
            }
            catch (Exception)
            {
                // TODO: OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Shutdown), ex);
            }

            tcs.TrySetResult(result);
            return result;
        }

        /// <summary>
        /// Attempts to shutdown the processor, blocks the current thread until
        /// shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety. Only the first call will
        /// win, subsequent calls will be no-op.
        /// </remarks>
        public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
        {
            Guard.InvalidTimeout(timeoutMilliseconds, nameof(timeoutMilliseconds));

            if (Interlocked.CompareExchange(ref this.shutdownCount, 1, 0) != 0)
            {
                return false; // shutdown already called
            }

            var result = false;
            try
            {
                result = this.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception)
            {
                // TODO: OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Shutdown), ex);
            }

            this.shutdownTcs.TrySetResult(result);
            return result;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        internal virtual void SetParentProvider(BaseProvider parentProvider)
        {
            this.ParentProvider = parentProvider;
        }

        internal Metric AddMetricWithNoViews(Instrument instrument)
        {
            var metricName = instrument.Name;
            lock (this.instrumentCreationLock)
            {
                if (this.metricStreamNames.Contains(metricName))
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricName, instrument.Meter.Name, "Metric name conflicting with existing name.", "Either change the name of the instrument or change name using View.");
                    return null;
                }

                var index = ++this.metricIndex;
                if (index >= MaxMetrics)
                {
                    OpenTelemetrySdkEventSource.Log.MetricInstrumentIgnored(metricName, instrument.Meter.Name, "Maximum allowed Metrics for the provider exceeded.", "Use views to drop unused instruments. Or configure Provider to allow higher limit.");
                    return null;
                }
                else
                {
                    var metric = new Metric(instrument, this.preferredAggregationTemporality, metricName, instrument.Description);
                    this.metrics[index] = metric;
                    this.metricStreamNames.Add(metricName);
                    return metric;
                }
            }
        }

        internal void RecordSingleStreamLongMeasurement(Metric metric, long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            metric.UpdateLong(value, tags);
        }

        internal void RecordSingleStreamDoubleMeasurement(Metric metric, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            metric.UpdateDouble(value, tags);
        }

        internal List<Metric> AddMetricsListWithViews(Instrument instrument, List<MetricStreamConfiguration> metricStreamConfigs)
        {
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
                        metric = new Metric(instrument, this.preferredAggregationTemporality, metricStreamName, metricDescription, histogramBucketBounds, tagKeysInteresting);

                        this.metrics[index] = metric;
                        metrics.Add(metric);
                        this.metricStreamNames.Add(metricStreamName);
                    }
                }

                return metrics;
            }
        }

        internal void RecordLongMeasurement(List<Metric> metrics, long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            if (metrics.Count == 1)
            {
                // special casing the common path
                // as this is faster than the
                // foreach, when count is 1.
                metrics[0].UpdateLong(value, tags);
            }
            else
            {
                foreach (var metric in metrics)
                {
                    metric.UpdateLong(value, tags);
                }
            }
        }

        internal void RecordDoubleMeasurement(List<Metric> metrics, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            if (metrics.Count == 1)
            {
                // special casing the common path
                // as this is faster than the
                // foreach, when count is 1.
                metrics[0].UpdateDouble(value, tags);
            }
            else
            {
                foreach (var metric in metrics)
                {
                    metric.UpdateDouble(value, tags);
                }
            }
        }

        internal void CompleteSingleStreamMeasurement(Metric metric)
        {
            metric.InstrumentDisposed = true;
        }

        /// <summary>
        /// Processes a batch of metrics.
        /// </summary>
        /// <param name="metrics">Batch of metrics to be processed.</param>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when metrics processing succeeded; otherwise,
        /// <c>false</c>.
        /// </returns>
        protected abstract bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds);

        /// <summary>
        /// Called by <c>Collect</c>. This function should block the current
        /// thread until metrics collection completed, shutdown signaled or
        /// timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when metrics collection succeeded; otherwise,
        /// <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This function is called synchronously on the threads which called
        /// <c>Collect</c>. This function should not throw exceptions.
        /// </remarks>
        protected virtual bool OnCollect(int timeoutMilliseconds)
        {
            var sw = Stopwatch.StartNew();

            var collectObservableInstruments = this.ParentProvider.GetCollectObservableInstruments();
            collectObservableInstruments();

            var metrics = this.GetMetricsBatch();

            if (timeoutMilliseconds == Timeout.Infinite)
            {
                return this.ProcessMetrics(metrics, Timeout.Infinite);
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                if (timeout <= 0)
                {
                    return false;
                }

                return this.ProcessMetrics(metrics, (int)timeout);
            }
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
        protected virtual bool OnShutdown(int timeoutMilliseconds)
        {
            return this.Collect(timeoutMilliseconds);
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally
        /// releases the managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true"/> to release both managed and unmanaged resources;
        /// <see langword="false"/> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
        }

        private static void ValidateAggregationTemporality(AggregationTemporality preferred, AggregationTemporality supported)
        {
            Guard.Zero((int)(preferred & CumulativeAndDelta), $"PreferredAggregationTemporality has an invalid value {preferred}", nameof(preferred));
            Guard.Zero((int)(supported & CumulativeAndDelta), $"SupportedAggregationTemporality has an invalid value {supported}", nameof(supported));

            /*
            | Preferred  | Supported  | Valid |
            | ---------- | ---------- | ----- |
            | Both       | Both       | true  |
            | Both       | Cumulative | false |
            | Both       | Delta      | false |
            | Cumulative | Both       | true  |
            | Cumulative | Cumulative | true  |
            | Cumulative | Delta      | false |
            | Delta      | Both       | true  |
            | Delta      | Cumulative | false |
            | Delta      | Delta      | true  |
            */
            string message = $"PreferredAggregationTemporality {preferred} and SupportedAggregationTemporality {supported} are incompatible";
            Guard.Zero((int)(preferred & supported), message, nameof(preferred));
            Guard.Range((int)preferred, nameof(preferred), max: (int)supported, maxName: nameof(supported), message: message);
        }

        private Batch<Metric> GetMetricsBatch()
        {
            try
            {
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
}
