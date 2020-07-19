// <copyright file="BatchingActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace.Export
{
    /// <summary>
    /// Implements processor that batches activities before calling exporter.
    /// </summary>
    public class BatchingActivityProcessor : ActivityProcessor, IDisposable
    {
        private const int DefaultMaxQueueSize = 2048;
        private const int DefaultMaxExportBatchSize = 512;
        private static readonly TimeSpan DefaultScheduledDelay = TimeSpan.FromMilliseconds(5000);
        private static readonly TimeSpan DefaultExporterTimeout = TimeSpan.FromMilliseconds(30000);

        private readonly ConcurrentQueue<Activity> exportQueue;
        private readonly int maxQueueSize;
        private readonly int maxExportBatchSize;
        private readonly TimeSpan scheduledDelay;
        private readonly TimeSpan exporterTimeout;
        private readonly ActivityExporter exporter;
        private readonly List<Activity> batch = new List<Activity>();
        private readonly SemaphoreSlim flushLock = new SemaphoreSlim(1);
        private readonly System.Timers.Timer flushTimer;
        private volatile int currentQueueSize;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchingActivityProcessor"/> class with default parameters:
        /// <list type="bullet">
        /// <item>
        /// <description>maxQueueSize = 2048,</description>
        /// </item>
        /// <item>
        /// <description>scheduledDelay = 5 sec,</description>
        /// </item>
        /// <item>
        /// <description>exporterTimeout = 30 sec,</description>
        /// </item>
        /// <item>
        /// <description>maxExportBatchSize = 512</description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        public BatchingActivityProcessor(ActivityExporter exporter)
            : this(exporter, DefaultMaxQueueSize, DefaultScheduledDelay, DefaultExporterTimeout, DefaultMaxExportBatchSize)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchingActivityProcessor"/> class with custom settings.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        /// <param name="maxQueueSize">Maximum queue size. After the size is reached activities are dropped by processor.</param>
        /// <param name="scheduledDelay">The delay between two consecutive exports.</param>
        /// <param name="exporterTimeout">Maximum allowed time to export data.</param>
        /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize.</param>
        public BatchingActivityProcessor(ActivityExporter exporter, int maxQueueSize, TimeSpan scheduledDelay, TimeSpan exporterTimeout, int maxExportBatchSize)
        {
            if (maxQueueSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxQueueSize));
            }

            if (maxExportBatchSize <= 0 || maxExportBatchSize > maxQueueSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExportBatchSize));
            }

            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            this.maxQueueSize = maxQueueSize;
            this.scheduledDelay = scheduledDelay;
            this.exporterTimeout = exporterTimeout;
            this.maxExportBatchSize = maxExportBatchSize;

            this.exportQueue = new ConcurrentQueue<Activity>();

            this.flushTimer = new System.Timers.Timer
            {
                AutoReset = false,
                Enabled = true,
                Interval = this.scheduledDelay.TotalMilliseconds,
            };

            this.flushTimer.Elapsed += async (sender, args) =>
            {
                await this.FlushAsyncInternal(drain: false, CancellationToken.None).ConfigureAwait(false);
            };
        }

        /// <inheritdoc/>
        public override void OnStart(Activity activity)
        {
        }

        /// <inheritdoc/>
        public override void OnEnd(Activity activity)
        {
            // because of race-condition between checking the size and enqueueing,
            // we might end up with a bit more activities than maxQueueSize.
            // Let's just tolerate it to avoid extra synchronization.
            if (this.currentQueueSize >= this.maxQueueSize)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorQueueIsExhausted();
                return;
            }

            Interlocked.Increment(ref this.currentQueueSize);

            this.exportQueue.Enqueue(activity);
        }

        /// <inheritdoc/>
        public override async Task ShutdownAsync(CancellationToken cancellationToken)
        {
            await this.FlushAsyncInternal(drain: true, cancellationToken).ConfigureAwait(false);

            await this.exporter.ShutdownAsync(cancellationToken).ConfigureAwait(false);

            OpenTelemetrySdkEventSource.Log.ShutdownEvent(this.currentQueueSize);
        }

        /// <inheritdoc/>
        public override async Task ForceFlushAsync(CancellationToken cancellationToken)
        {
            await this.FlushAsyncInternal(drain: true, cancellationToken).ConfigureAwait(false);

            OpenTelemetrySdkEventSource.Log.ForceFlushCompleted(this.currentQueueSize);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            try
            {
                this.ShutdownAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
            }

            if (isDisposing && !this.isDisposed)
            {
                if (this.exporter is IDisposable disposableExporter)
                {
                    try
                    {
                        disposableExporter.Dispose();
                    }
                    catch (Exception e)
                    {
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), e);
                    }
                }

                this.flushTimer.Dispose();
                this.flushLock.Dispose();

                this.isDisposed = true;
            }
        }

        private async Task FlushAsyncInternal(bool drain, CancellationToken cancellationToken)
        {
            await this.flushLock.WaitAsync().ConfigureAwait(false);

            try
            {
                this.flushTimer.Enabled = false;

                var queueSize = this.currentQueueSize;
                do
                {
                    var exported = await this.ExportBatchAsync(cancellationToken).ConfigureAwait(false);

                    if (exported == 0)
                    {
                        // Break out of drain loop if nothing is being exported, likely means there is an issue
                        // and we don't want to deadlock.
                        break;
                    }

                    queueSize -= exported;
                }
#pragma warning disable SA1009 // Closing parenthesis should be spaced correctly -> Spacing is for readability
                while (
                    !cancellationToken.IsCancellationRequested &&
                    (
                        (drain && queueSize > 0) // If draining, keep looping until queue is empty.
                      ||
                        (!drain && queueSize >= this.maxExportBatchSize) // If not draining, keep looping while there are batches ready.
                    ));
#pragma warning restore SA1009 // Closing parenthesis should be spaced correctly
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.FlushAsyncInternal), ex);
            }
            finally
            {
                this.flushTimer.Enabled = true;

                this.flushLock.Release();
            }
        }

        private async Task<int> ExportBatchAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (this.exportQueue.TryDequeue(out var nextActivity))
                {
                    Interlocked.Decrement(ref this.currentQueueSize);
                    this.batch.Add(nextActivity);
                }
                else
                {
                    // nothing in queue
                    return 0;
                }

                while (this.batch.Count < this.maxExportBatchSize && this.exportQueue.TryDequeue(out nextActivity))
                {
                    Interlocked.Decrement(ref this.currentQueueSize);
                    this.batch.Add(nextActivity);
                }

                var result = await this.exporter.ExportAsync(this.batch, cancellationToken).ConfigureAwait(false);
                if (result != ExportResult.Success)
                {
                    OpenTelemetrySdkEventSource.Log.ExporterErrorResult(result);

                    // we do not support retries for now and leave it up to exporter
                    // as only exporter implementation knows how to retry: which items failed
                    // and what is the reasonable policy for that exporter.
                }

                return this.batch.Count;
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.ExportBatchAsync), ex);
                return 0;
            }
            finally
            {
                this.batch.Clear();
            }
        }
    }
}
