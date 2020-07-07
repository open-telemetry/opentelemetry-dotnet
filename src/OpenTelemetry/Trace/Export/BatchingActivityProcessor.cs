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
        private CancellationTokenSource cts;
        private volatile int currentQueueSize;
        private bool stopping = false;

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

            this.cts = new CancellationTokenSource();
            this.exportQueue = new ConcurrentQueue<Activity>();

            // worker task that will last for lifetime of processor.
            // Threads are also useless as exporter tasks run in thread pool threads.
            Task.Run(() => this.Worker(this.cts.Token), this.cts.Token);
        }

        /// <inheritdoc/>
        public override void OnStart(Activity activity)
        {
        }

        /// <inheritdoc/>
        public override void OnEnd(Activity activity)
        {
            if (this.stopping)
            {
                return;
            }

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
            if (!this.stopping)
            {
                this.stopping = true;

                // This will stop the loop after current batch finishes.
                this.cts.Cancel(false);
                this.cts.Dispose();
                this.cts = null;

                // if there are more items, continue until cancellation token allows
                while (this.currentQueueSize > 0 && !cancellationToken.IsCancellationRequested)
                {
                    await this.ExportBatchAsync(cancellationToken).ConfigureAwait(false);
                }

                await this.exporter.ShutdownAsync(cancellationToken);

                // there is no point in waiting for a worker task if cancellation happens
                // it's dead already or will die on the next iteration on its own

                // ExportBatchAsync must never throw, we are here either because it was cancelled
                // or because there are no items left
                OpenTelemetrySdkEventSource.Log.ShutdownEvent(this.currentQueueSize);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (!this.stopping)
            {
                this.ShutdownAsync(CancellationToken.None).ContinueWith(_ => { }).GetAwaiter().GetResult();
            }

            if (isDisposing)
            {
                if (this.exporter is IDisposable disposableExporter)
                {
                    try
                    {
                        disposableExporter.Dispose();
                    }
                    catch (Exception e)
                    {
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException("Dispose", e);
                    }
                }
            }
        }

        private async Task ExportBatchAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (this.exportQueue.TryDequeue(out var nextActivity))
                {
                    Interlocked.Decrement(ref this.currentQueueSize);
                    this.batch.Add(nextActivity);
                }
                else
                {
                    // nothing in queue
                    return;
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
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.ExportBatchAsync), ex);
            }
            finally
            {
                this.batch.Clear();
            }
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                using (var exportCancellationTokenSource = new CancellationTokenSource(this.exporterTimeout))
                {
                    await this.ExportBatchAsync(exportCancellationTokenSource.Token).ConfigureAwait(false);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var remainingWait = this.scheduledDelay - sw.Elapsed;
                if (remainingWait > TimeSpan.Zero)
                {
                    await Task.Delay(remainingWait, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
