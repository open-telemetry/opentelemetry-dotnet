// <copyright file="BatchExportProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry
{
    /// <summary>
    /// Implements processor that batches telemetry objects before calling exporter.
    /// </summary>
    /// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
    public abstract class BatchExportProcessor<T> : BaseExportProcessor<T>
        where T : class
    {
        internal const int DefaultMaxQueueSize = 2048;
        internal const int DefaultScheduledDelayMilliseconds = 5000;
        internal const int DefaultExporterTimeoutMilliseconds = 30000;
        internal const int DefaultMaxExportBatchSize = 512;

        private readonly CircularBuffer<T> circularBuffer;
        private readonly int scheduledDelayMilliseconds;
        private readonly int exporterTimeoutMilliseconds;
        private readonly int maxExportBatchSize;
        private readonly Thread exporterThread;
        private readonly AutoResetEvent exportTrigger = new AutoResetEvent(false);
        private readonly ManualResetEvent dataExportedNotification = new ManualResetEvent(false);
        private readonly ManualResetEvent shutdownTrigger = new ManualResetEvent(false);
        private long shutdownDrainTarget = long.MaxValue;
        private long droppedCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchExportProcessor{T}"/> class.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
        /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
        /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
        /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
        protected BatchExportProcessor(
            BaseExporter<T> exporter,
            int maxQueueSize = DefaultMaxQueueSize,
            int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
            int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
            int maxExportBatchSize = DefaultMaxExportBatchSize)
            : base(exporter)
        {
            if (maxQueueSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxQueueSize), maxQueueSize, "maxQueueSize should be greater than zero.");
            }

            if (maxExportBatchSize <= 0 || maxExportBatchSize > maxQueueSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExportBatchSize), maxExportBatchSize, "maxExportBatchSize should be greater than zero and less than maxQueueSize.");
            }

            if (scheduledDelayMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scheduledDelayMilliseconds), scheduledDelayMilliseconds, "scheduledDelayMilliseconds should be greater than zero.");
            }

            if (exporterTimeoutMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exporterTimeoutMilliseconds), exporterTimeoutMilliseconds, "exporterTimeoutMilliseconds should be non-negative.");
            }

            this.circularBuffer = new CircularBuffer<T>(maxQueueSize);
            this.scheduledDelayMilliseconds = scheduledDelayMilliseconds;
            this.exporterTimeoutMilliseconds = exporterTimeoutMilliseconds;
            this.maxExportBatchSize = maxExportBatchSize;
            this.exporterThread = new Thread(new ThreadStart(this.ExporterProc))
            {
                IsBackground = true,
                Name = $"OpenTelemetry-{nameof(BatchExportProcessor<T>)}-{exporter.GetType().Name}",
            };
            this.exporterThread.Start();
        }

        /// <summary>
        /// Gets the number of telemetry objects dropped by the processor.
        /// </summary>
        internal long DroppedCount => this.droppedCount;

        /// <summary>
        /// Gets the number of telemetry objects received by the processor.
        /// </summary>
        internal long ReceivedCount => this.circularBuffer.AddedCount + this.DroppedCount;

        /// <summary>
        /// Gets the number of telemetry objects processed by the underlying exporter.
        /// </summary>
        internal long ProcessedCount => this.circularBuffer.RemovedCount;

        /// <inheritdoc/>
        protected override void OnExport(T data)
        {
            if (this.circularBuffer.TryAdd(data, maxSpinCount: 50000))
            {
                if (this.circularBuffer.Count >= this.maxExportBatchSize)
                {
                    this.exportTrigger.Set();
                }

                return; // enqueue succeeded
            }

            // either the queue is full or exceeded the spin limit, drop the item on the floor
            Interlocked.Increment(ref this.droppedCount);
        }

        /// <inheritdoc/>
        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            var tail = this.circularBuffer.RemovedCount;
            var head = this.circularBuffer.AddedCount;

            if (head == tail)
            {
                return true; // nothing to flush
            }

            this.exportTrigger.Set();

            if (timeoutMilliseconds == 0)
            {
                return false;
            }

            var triggers = new WaitHandle[] { this.dataExportedNotification, this.shutdownTrigger };

            var sw = Stopwatch.StartNew();

            // There is a chance that the export thread finished processing all the data from the queue,
            // and signaled before we enter wait here, use polling to prevent being blocked indefinitely.
            const int pollingMilliseconds = 1000;

            while (true)
            {
                if (timeoutMilliseconds == Timeout.Infinite)
                {
                    WaitHandle.WaitAny(triggers, pollingMilliseconds);
                }
                else
                {
                    var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                    if (timeout <= 0)
                    {
                        return this.circularBuffer.RemovedCount >= head;
                    }

                    WaitHandle.WaitAny(triggers, Math.Min((int)timeout, pollingMilliseconds));
                }

                if (this.circularBuffer.RemovedCount >= head)
                {
                    return true;
                }

                if (this.shutdownDrainTarget != long.MaxValue)
                {
                    return false;
                }
            }
        }

        /// <inheritdoc/>
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            this.shutdownDrainTarget = this.circularBuffer.AddedCount;
            this.shutdownTrigger.Set();

            if (timeoutMilliseconds == Timeout.Infinite)
            {
                this.exporterThread.Join();
                return this.exporter.Shutdown();
            }

            if (timeoutMilliseconds == 0)
            {
                return this.exporter.Shutdown(0);
            }

            var sw = Stopwatch.StartNew();
            this.exporterThread.Join(timeoutMilliseconds);
            var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
            return this.exporter.Shutdown((int)Math.Max(timeout, 0));
        }

        private void ExporterProc()
        {
            var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };

            while (true)
            {
                // only wait when the queue doesn't have enough items, otherwise keep busy and send data continuously
                if (this.circularBuffer.Count < this.maxExportBatchSize)
                {
                    WaitHandle.WaitAny(triggers, this.scheduledDelayMilliseconds);
                }

                if (this.circularBuffer.Count > 0)
                {
                    using (var batch = new Batch<T>(this.circularBuffer, this.maxExportBatchSize))
                    {
                        this.exporter.Export(batch);
                    }

                    this.dataExportedNotification.Set();
                    this.dataExportedNotification.Reset();
                }

                if (this.circularBuffer.RemovedCount >= this.shutdownDrainTarget)
                {
                    break;
                }
            }
        }
    }
}
