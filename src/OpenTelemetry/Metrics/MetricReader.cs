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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public abstract class MetricReader : IDisposable
    {
        private const AggregationTemporality CumulativeAndDelta = AggregationTemporality.Cumulative | AggregationTemporality.Delta;
        private readonly object newTaskLock = new object();
        private readonly object onCollectLock = new object();
        private readonly TaskCompletionSource<bool> shutdownTcs = new TaskCompletionSource<bool>();
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
            var sw = timeoutMilliseconds == Timeout.Infinite
                ? null
                : Stopwatch.StartNew();

            var collectMetric = this.ParentProvider.GetMetricCollect();
            var metrics = collectMetric();

            if (sw == null)
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
    }
}
