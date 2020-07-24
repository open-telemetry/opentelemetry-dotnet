// <copyright file="SimpleActivityProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Implements simple activity processor that exports activities in OnEnd call without batching.
    /// </summary>
    public class SimpleActivityProcessor : ActivityProcessor, IDisposable
    {
        private const int DefaultMaxQueueSize = 2048;
        private const int DefaultMaxExportBatchSize = 512;
        private static readonly TimeSpan DefaultExporterTimeout = TimeSpan.FromMilliseconds(30000);

        private readonly ActivityExporter exporter;
        private readonly int maxQueueSize;
        private readonly TimeSpan exporterTimeout;
        private readonly int maxExportBatchSize;
        private readonly ConcurrentQueue<Activity> activityQueue;
        private readonly EventWaitHandle stopHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private readonly EventWaitHandle dataReadyHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        private readonly Thread backgroundThread;
        private volatile int currentQueueSize;
        private bool stopped;

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleActivityProcessor"/> class with default parameters:
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
        public SimpleActivityProcessor(ActivityExporter exporter)
            : this(exporter, DefaultMaxQueueSize, DefaultExporterTimeout, DefaultMaxExportBatchSize)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleActivityProcessor"/> class.
        /// </summary>
        /// <param name="exporter">Activity exporter instance.</param>
        /// <param name="maxQueueSize">Maximum queue size. After the size is reached activities are dropped by processor.</param>
        /// <param name="exporterTimeout">Maximum allowed time to export data.</param>
        /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize.</param>
        public SimpleActivityProcessor(ActivityExporter exporter, int maxQueueSize, TimeSpan exporterTimeout, int maxExportBatchSize)
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
            this.exporterTimeout = exporterTimeout;
            this.maxExportBatchSize = maxExportBatchSize;

            this.backgroundThread = new Thread(this.BackgroundThreadBody)
            {
                Name = "OpenTelemetry.Processor",
            };
            this.backgroundThread.Start();
        }

        /// <inheritdoc />
        public override void OnStart(Activity activity)
        {
        }

        /// <inheritdoc />
        public override void OnEnd(Activity activity)
        {
            if (this.currentQueueSize >= this.maxQueueSize)
            {
                OpenTelemetrySdkEventSource.Log.SpanProcessorQueueIsExhausted();
                return;
            }

            Interlocked.Increment(ref this.currentQueueSize);

            this.activityQueue.Enqueue(activity);
            this.dataReadyHandle.Set();
        }

        /// <inheritdoc />
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            if (!this.stopped)
            {
                this.stopped = true;

                this.stopHandle.Set();
                this.backgroundThread.Join();

                return this.exporter.ShutdownAsync(cancellationToken);
            }

#if NET452
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

        /// <inheritdoc />
        public override Task ForceFlushAsync(CancellationToken cancellationToken)
        {
#if NET452
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
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
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), e);
                    }

                    this.stopHandle.Dispose();
                    this.dataReadyHandle.Dispose();
                }
            }
        }

        private void BackgroundThreadBody(object state)
        {
            WaitHandle[] handles = new WaitHandle[] { this.stopHandle, this.dataReadyHandle };
            List<Activity> activities = new List<Activity>(this.maxExportBatchSize);

            while (true)
            {
                int handleIndex = WaitHandle.WaitAny(handles);

                try
                {
                    while (true)
                    {
                        // Read off the queue data that is ready to transmit, up to maxExportBatchSize.
                        while (this.activityQueue.TryDequeue(out Activity activity))
                        {
                            Interlocked.Decrement(ref this.currentQueueSize);
                            activities.Add(activity);
                            if (activities.Count == this.maxExportBatchSize)
                            {
                                break;
                            }
                        }

                        if (activities.Count == 0)
                        {
                            // No work to do, wait for signal to start again.
                            break;
                        }

                        using var cts = new CancellationTokenSource(this.exporterTimeout);
                        try
                        {
                            this.exporter.ExportAsync(activities, cts.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            OpenTelemetrySdkEventSource.Log.SpanExporterTimeout(activities.Count);
                        }
                        finally
                        {
                            activities.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.BackgroundThreadBody), ex);
                }

                if (handleIndex == 0)
                {
                    // If shutdown was requested, exit thread.
                    return;
                }
            }
        }
    }
}
