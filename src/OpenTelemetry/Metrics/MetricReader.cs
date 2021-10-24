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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public abstract class MetricReader : IDisposable
    {
        private const AggregationTemporality CumulativeAndDelta = AggregationTemporality.Cumulative | AggregationTemporality.Delta;
        private readonly object newJobLock = new object();
        private readonly object onCollectLock = new object();
        private readonly ManualResetEvent collectCompletedNotification = new ManualResetEvent(false);
        private readonly ManualResetEvent shutdownTrigger = new ManualResetEvent(false);
        private readonly WaitHandle[] triggers;
        private AggregationTemporality preferredAggregationTemporality = CumulativeAndDelta;
        private AggregationTemporality supportedAggregationTemporality = CumulativeAndDelta;
        private long scheduledJobCount;
        private long completedJobCount;
        private int shutdownCount;
        private Job<bool> collectionJob;
        private bool disposed;

        protected MetricReader()
        {
            this.triggers = new WaitHandle[] { this.collectCompletedNotification, this.shutdownTrigger };
        }

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
        /// metrics collection completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number (non-negative) of milliseconds to wait, or
        /// <c>Timeout.Infinite</c> to wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when metrics collection succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the <c>timeoutMilliseconds</c> is smaller than -1.
        /// </exception>
        /// <remarks>
        /// This function guarantees thread-safety.
        /// </remarks>
        public bool Collect(int timeoutMilliseconds = Timeout.Infinite)
        {
            Guard.InvalidTimeout(timeoutMilliseconds, nameof(timeoutMilliseconds));

            var job = this.collectionJob;
            var shouldDoJob = false;

            if (job == null)
            {
                lock (this.newJobLock)
                {
                    job = this.collectionJob;

                    if (job == null)
                    {
                        shouldDoJob = true;
                        job = new Job<bool>(Interlocked.Increment(ref this.scheduledJobCount));
                        this.collectionJob = job;
                    }
                }
            }

            if (!shouldDoJob)
            {
                // There is a chance that the collect thread finished processing all the data, and signaled
                // before we enter wait here, use polling to prevent being blocked indefinitely.
                const int pollingMilliseconds = 1000;

                var sw = Stopwatch.StartNew();

                while (true)
                {
                    if (timeoutMilliseconds == Timeout.Infinite)
                    {
                        WaitHandle.WaitAny(this.triggers, pollingMilliseconds);
                    }
                    else
                    {
                        var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                        if (timeout <= 0)
                        {
                            return Interlocked.Read(ref this.completedJobCount) >= job.Id ? job.Result : false;
                        }

                        WaitHandle.WaitAny(this.triggers, Math.Min((int)timeout, pollingMilliseconds));
                    }

                    if (Interlocked.Read(ref this.completedJobCount) >= job.Id)
                    {
                        return job.Result;
                    }
                }
            }

            try
            {
                lock (this.onCollectLock)
                {
                    this.collectionJob = null;
                    job.Result = this.OnCollect(timeoutMilliseconds);
                }
            }
            catch (Exception)
            {
                // TODO: OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Collect), ex);
            }
            finally
            {
                Interlocked.Increment(ref this.completedJobCount);
                this.collectCompletedNotification.Set();
                this.collectCompletedNotification.Reset();
            }

            return job.Result;
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

            try
            {
                return this.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception)
            {
                // TODO: OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Shutdown), ex);
                return false;
            }
            finally
            {
                this.shutdownTrigger.Set();
            }
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
        protected abstract bool ProcessMetrics(Batch<Metric> metrics, int timeoutMilliseconds);

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
        /// This function is called synchronously on the thread which called
        /// <c>Collect</c>. This function should be thread-safe, and should
        /// not throw exceptions.
        /// </remarks>
        protected virtual bool OnCollect(int timeoutMilliseconds)
        {
            var sw = Stopwatch.StartNew();

            var collectMetric = this.ParentProvider.GetMetricCollect();
            var metrics = collectMetric();

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
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                this.collectCompletedNotification.Dispose();
                this.shutdownTrigger.Dispose();
            }

            this.disposed = true;
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

        private class Job<T>
        {
            private long id;

            public Job(long id)
            {
                this.id = id;
            }

            public long Id => this.id;

            public T Result { get; set; }
        }
    }
}
