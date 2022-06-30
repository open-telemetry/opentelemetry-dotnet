// <copyright file="BatchLogRecordExportProcessor.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Diagnostics;
using OpenTelemetry.Logs;

namespace OpenTelemetry
{
    /// <summary>
    /// Implements a batch log record export processor.
    /// </summary>
    public class BatchLogRecordExportProcessor : BatchExportProcessor<LogRecord>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BatchLogRecordExportProcessor"/> class.
        /// </summary>
        /// <param name="exporter">Log record exporter.</param>
        /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
        /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
        /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
        /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
        public BatchLogRecordExportProcessor(
            BaseExporter<LogRecord> exporter,
            int maxQueueSize = DefaultMaxQueueSize,
            int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
            int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
            int maxExportBatchSize = DefaultMaxExportBatchSize)
            : base(
                exporter,
                maxQueueSize,
                scheduledDelayMilliseconds,
                exporterTimeoutMilliseconds,
                maxExportBatchSize)
        {
        }

        /// <inheritdoc/>
        public override void OnEnd(LogRecord data)
        {
            // Note: Intentionally doing a Debug.Assert here and not a
            // Guard.ThrowIfNull to save prod cycles. Null should really never
            // happen here.
            Debug.Assert(data != null, "LogRecord was null.");

            data!.Buffer();

            data.AddReference();

            if (!this.TryExport(data))
            {
                LogRecordSharedPool.Current.Return(data);
            }
        }
    }
}
