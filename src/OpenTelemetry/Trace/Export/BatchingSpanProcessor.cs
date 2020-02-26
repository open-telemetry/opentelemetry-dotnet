// <copyright file="BatchingSpanProcessor.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
    /// Implements span processor that batches spans before calling exporter.
    /// </summary>
    public class BatchingSpanProcessor : SpanProcessor, IDisposable
    {
        private const int DefaultMaxQueueSize = 2048;
        private const int DefaultMaxExportBatchSize = 512;
        private static readonly TimeSpan DefaultScheduleDelay = TimeSpan.FromMilliseconds(5000);
        private readonly ConcurrentQueue<SpanData> exportQueue;
        private readonly int maxQueueSize;
        private readonly int maxExportBatchSize;
        private readonly TimeSpan scheduleDelay;
        private readonly SpanExporter exporter;
        private CancellationTokenSource cts;
        private volatile int currentQueueSize;
        private bool stopping = false;

        /// <summary>
        /// Constructs batching processor with default parameters:
        /// <list type="bullet">
        /// <item>
        /// <description>maxQueueSize = 2048,</description>
        /// </item>
        /// <item>
        /// <description>scheduleDelay = 5 sec,</description>
        /// </item>
        /// <item>
        /// <description>maxExportBatchSize = 512</description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        public BatchingSpanProcessor(SpanExporter exporter) : this(exporter, DefaultMaxQueueSize, DefaultScheduleDelay, DefaultMaxExportBatchSize)
        {
        }

        /// <summary>
        /// Constructs batching processor with custom settings.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        /// <param name="maxQueueSize">Maximum queue size. After the size is reached spans are dropped by processor.</param>
        /// <param name="scheduleDelay">The delay between two consecutive exports.</param>
        /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize.</param>
        public BatchingSpanProcessor(SpanExporter exporter, int maxQueueSize, TimeSpan scheduleDelay, int maxExportBatchSize)
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
            this.scheduleDelay = scheduleDelay;
            this.maxExportBatchSize = maxExportBatchSize;

            this.cts = new CancellationTokenSource();
            this.exportQueue = new ConcurrentQueue<SpanData>();

            // worker task that will last for lifetime of processor.
            // No need to specify long running - it is useless if any async calls are made internally.
            // Threads are also useless as exporter tasks run in thread pool threads.
            Task.Factory.StartNew(s => this.Worker((CancellationToken)s), this.cts.Token);
        }

        public override void OnStart(SpanData span)
        {
        }

        public override void OnEnd(SpanData span)
        {
            if (this.stopping)
            {
                return;
            }

            // because of race-condition between checking the size and enqueueing,
            // we might end up with a bit more spans than maxQueueSize.
            // Let's just tolerate it to avoid extra synchronization.
            if (this.currentQueueSize >= this.maxQueueSize)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorQueueIsExhausted();
                return;
            }

            Interlocked.Increment(ref this.currentQueueSize);

            this.exportQueue.Enqueue(span);
        }

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

                List<SpanData> batch = null;
                if (this.exportQueue.TryDequeue(out var nextSpan))
                {
                    Interlocked.Decrement(ref this.currentQueueSize);
                    batch = new List<SpanData> { nextSpan };
                }
                else
                {
                    // nothing in queue
                    return;
                }

                while (batch.Count < this.maxExportBatchSize && this.exportQueue.TryDequeue(out nextSpan))
                {
                    Interlocked.Decrement(ref this.currentQueueSize);
                    batch.Add(nextSpan);
                }

                var result = await this.exporter.ExportAsync(batch, cancellationToken).ConfigureAwait(false);
                if (result != SpanExporter.ExportResult.Success)
                {
                    OpenTelemetrySdkEventSource.Log.ExporterErrorResult(result);

                    // we do not support retries for now and leave it up to exporter
                    // as only exporter implementation knows how to retry: which items failed
                    // and what is the reasonable policy for that exporter.
                }
            }
            catch (Exception ex)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorException("OnStart", ex);
            }
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                await this.ExportBatchAsync(cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var remainingWait = this.scheduleDelay - sw.Elapsed;
                if (remainingWait > TimeSpan.Zero)
                {
                    await Task.Delay(remainingWait, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
