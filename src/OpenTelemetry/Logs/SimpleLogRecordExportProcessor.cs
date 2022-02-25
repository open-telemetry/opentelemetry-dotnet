// <copyright file="SimpleLogRecordExportProcessor.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Logs;

namespace OpenTelemetry
{
    public class SimpleLogRecordExportProcessor : SimpleExportProcessor<LogRecord>
    {
        public SimpleLogRecordExportProcessor(BaseExporter<LogRecord> exporter)
            : base(exporter)
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not log data should be buffered.
        /// </summary>
        /// <remarks>
        /// Note: Log state and scopes are only available during the lifecycle
        /// of the log message being written. If you need to capture data to be
        /// used later (for example in batching scenarios), set <see
        /// cref="BufferLogData"/> to <see langword="true"/>.
        /// </remarks>
        public bool BufferLogData { get; set; }

        /// <inheritdoc/>
        public override void OnEnd(LogRecord data)
        {
            if (this.BufferLogData)
            {
                data.Buffer();
            }

            base.OnEnd(data);
        }
    }
}
