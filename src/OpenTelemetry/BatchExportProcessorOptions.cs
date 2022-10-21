// <copyright file="BatchExportProcessorOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry
{
    public class BatchExportProcessorOptions<T>
        where T : class
    {
        /// <summary>
        /// Gets or sets the maximum queue size. The queue drops the data if the maximum size is reached. The default value is 2048.
        /// </summary>
        public int MaxQueueSize { get; set; } = BatchExportProcessor<T>.DefaultMaxQueueSize;

        /// <summary>
        /// Gets or sets the delay interval (in milliseconds) between two consecutive exports. The default value is 5000.
        /// </summary>
        public int ScheduledDelayMilliseconds { get; set; } = BatchExportProcessor<T>.DefaultScheduledDelayMilliseconds;

        /// <summary>
        /// Gets or sets the timeout (in milliseconds) after which the export is cancelled. The default value is 30000.
        /// </summary>
        public int ExporterTimeoutMilliseconds { get; set; } = BatchExportProcessor<T>.DefaultExporterTimeoutMilliseconds;

        /// <summary>
        /// Gets or sets the maximum batch size of every export. It must be smaller or equal to MaxQueueLength. The default value is 512.
        /// </summary>
        public int MaxExportBatchSize { get; set; } = BatchExportProcessor<T>.DefaultMaxExportBatchSize;
    }
}
