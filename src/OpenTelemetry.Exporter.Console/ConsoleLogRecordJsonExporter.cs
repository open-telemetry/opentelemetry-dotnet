// <copyright file="ConsoleLogRecordExporter.cs" company="OpenTelemetry Authors">
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

using System.Text.Json;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter
{
    public class ConsoleLogRecordJsonExporter : ConsoleExporter<LogRecord>
    {
        private const int RightPaddingLength = 35;
        private readonly object syncObject = new();
        private bool disposed;
        private string disposedStackTrace;
        private bool isDisposeMessageSent;
        private JsonSerializerOptions JsonSerializerOptions;

        public ConsoleLogRecordJsonExporter(ConsoleExporterOptions options, JsonSerializerOptions jsonSerializerOptions = null)
            : base(options)
        {
            this.JsonSerializerOptions = jsonSerializerOptions;
        }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            if (this.disposed)
            {
                if (!this.isDisposeMessageSent)
                {
                    lock (this.syncObject)
                    {
                        if (this.isDisposeMessageSent)
                        {
                            return ExportResult.Failure;
                        }

                        this.isDisposeMessageSent = true;
                    }

                    this.WriteLine("The console exporter is still being invoked after it has been disposed. This could be due to the application's incorrect lifecycle management of the LoggerFactory/OpenTelemetry .NET SDK.");
                    this.WriteLine(Environment.StackTrace);
                    this.WriteLine(Environment.NewLine + "Dispose was called on the following stack trace:");
                    this.WriteLine(this.disposedStackTrace);
                }

                return ExportResult.Failure;
            }

            foreach (var logRecord in batch)
            {
                var serializerLogRecord = JsonSerializer.Serialize(logRecord, this.JsonSerializerOptions);
                this.WriteLine(serializerLogRecord);
            }

            return ExportResult.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.disposed = true;
                this.disposedStackTrace = Environment.StackTrace;
            }

            base.Dispose(disposing);
        }
    }
}
