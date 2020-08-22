// <copyright file="BatchExportActivityProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Implements processor that batches activities before calling exporter.
    /// </summary>
    public class BatchExportActivityProcessor : ActivityProcessor
    {
        private readonly ActivityExporter exporter;
        private readonly CircularBuffer<Activity> circularBuffer;
        private readonly int scheduledDelayMillis;
        private readonly int exporterTimeoutMillis;
        private readonly int maxExportBatchSize;
        private readonly Thread exporterThread;
        private readonly AutoResetEvent exportTrigger = new AutoResetEvent(false);
        private readonly ManualResetEvent dataExportedNotification = new ManualResetEvent(false);
        private readonly ManualResetEvent shutdownTrigger = new ManualResetEvent(false);
        private long shutdownDrainTarget = long.MaxValue;
        private bool disposed;
        private long droppedCount = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchExportActivityProcessor"/> class with custom settings.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
        /// <param name="scheduledDelayMillis">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
        /// <param name="exporterTimeoutMillis">How long the export can run before it is cancelled. The default value is 30000.</param>
        /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
        public BatchExportActivityProcessor(
            ActivityExporter exporter,
            int maxQueueSize = 2048,
            int scheduledDelayMillis = 5000,
            int exporterTimeoutMillis = 30000,
            int maxExportBatchSize = 512)
        {
            if (maxQueueSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxQueueSize));
            }

            if (maxExportBatchSize <= 0 || maxExportBatchSize > maxQueueSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExportBatchSize));
            }

            if (scheduledDelayMillis <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scheduledDelayMillis));
            }

            if (exporterTimeoutMillis < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exporterTimeoutMillis));
            }

            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            this.circularBuffer = new CircularBuffer<Activity>(maxQueueSize);
            this.scheduledDelayMillis = scheduledDelayMillis;
            this.exporterTimeoutMillis = exporterTimeoutMillis;
            this.maxExportBatchSize = maxExportBatchSize;
            this.exporterThread = new Thread(new ThreadStart(this.ExporterProc))
            {
                IsBackground = true,
                Name = $"OpenTelemetry-{nameof(BatchExportActivityProcessor)}-{exporter.GetType().Name}",
            };
            this.exporterThread.Start();
        }

        /// <summary>
        /// Gets the number of <see cref="Activity"/> dropped (when the queue is full).
        /// </summary>
        internal long DroppedCount
        {
            get
            {
                return this.droppedCount;
            }
        }

        /// <summary>
        /// Gets the number of <see cref="Activity"/> received by the processor.
        /// </summary>
        internal long ReceivedCount
        {
            get
            {
                return this.circularBuffer.AddedCount + this.DroppedCount;
            }
        }

        /// <summary>
        /// Gets the number of <see cref="Activity"/> processed by the underlying exporter.
        /// </summary>
        internal long ProcessedCount
        {
            get
            {
                return this.circularBuffer.RemovedCount;
            }
        }

        /// <inheritdoc/>
        public override void OnEnd(Activity activity)
        {
            if (this.circularBuffer.TryAdd(activity, maxSpinCount: 50000))
            {
                if (this.circularBuffer.Count >= this.maxExportBatchSize)
                {
                    this.exportTrigger.Set();
                }

                return; // enqueue succeeded
            }

            // either queue is full or exceeded spin count, drop item on the floor
            Interlocked.Increment(ref this.droppedCount);
        }

        /// <summary>
        /// Flushes the <see cref="Activity"/> currently in the queue, blocks
        /// the current thread until flush completed, shutdown signaled or
        /// timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when flush completed; otherwise, <c>false</c>.
        /// </returns>
        public override bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
        {
            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            }

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
            const int pollingMillis = 1000;

            while (true)
            {
                if (timeoutMilliseconds == Timeout.Infinite)
                {
                    WaitHandle.WaitAny(triggers, pollingMillis);
                }
                else
                {
                    var timeout = (long)timeoutMilliseconds - sw.ElapsedMilliseconds;

                    if (timeout <= 0)
                    {
                        return this.circularBuffer.RemovedCount >= head;
                    }

                    WaitHandle.WaitAny(triggers, Math.Min((int)timeout, pollingMillis));
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

        /// <summary>
        /// Attempts to drain the queue and shutdown the exporter, blocks the
        /// current thread until shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        public override void Shutdown(int timeoutMilliseconds = Timeout.Infinite)
        {
            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            }

            this.shutdownDrainTarget = this.circularBuffer.AddedCount;
            this.shutdownTrigger.Set();

            if (timeoutMilliseconds != 0)
            {
                this.exporterThread.Join(timeoutMilliseconds);
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !this.disposed)
            {
                // TODO: Dispose/Shutdown flow needs to be redesigned, currently it is convoluted.
                this.Shutdown(this.exporterTimeoutMillis);

                try
                {
                    this.exporter.Dispose();
                }
                catch (Exception ex)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                }

                this.disposed = true;
            }
        }

        private void ExporterProc()
        {
            var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };

            while (true)
            {
                // only wait when the queue doesn't have enough items, otherwise keep busy and send data continuously
                if (this.circularBuffer.Count < this.maxExportBatchSize)
                {
                    WaitHandle.WaitAny(triggers, this.scheduledDelayMillis);
                }

                if (this.circularBuffer.Count > 0)
                {
                    this.exporter.Export(new Batch<Activity>(this.circularBuffer, this.maxExportBatchSize));

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
