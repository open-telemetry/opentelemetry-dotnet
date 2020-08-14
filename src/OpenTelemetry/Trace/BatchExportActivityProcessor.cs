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
        private readonly ActivityExporterSync exporter;
        private readonly int maxQueueSize;
        private readonly TimeSpan scheduledDelay;
        private readonly TimeSpan exporterTimeout;
        private readonly int maxExportBatchSize;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BatchExportActivityProcessor"/> class with custom settings.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
        /// <param name="scheduledDelayMillis">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
        /// <param name="exporterTimeoutMillis">How long the export can run before it is cancelled. The default value is 30000.</param>
        /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
        public BatchExportActivityProcessor(
            ActivityExporterSync exporter,
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
            this.maxQueueSize = maxQueueSize;
            this.scheduledDelay = TimeSpan.FromMilliseconds(scheduledDelayMillis);
            this.exporterTimeout = TimeSpan.FromMilliseconds(exporterTimeoutMillis);
            this.maxExportBatchSize = maxExportBatchSize;
        }

        /// <inheritdoc/>
        public override void OnEnd(Activity activity)
        {
            // TODO
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        /// <exception cref="OperationCanceledException">If the <paramref name="cancellationToken"/> is canceled.</exception>
        public override Task ForceFlushAsync(CancellationToken cancellationToken)
        {
            // TODO
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        /// <exception cref="OperationCanceledException">If the <paramref name="cancellationToken"/> is canceled.</exception>
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            // TODO
            throw new NotImplementedException();
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
    }
}
