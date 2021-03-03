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

#if NET461 || NETSTANDARD2_0
using System;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Exporter
{
    public class ConsoleLogRecordExporter : ConsoleExporter<LogRecord>
    {
        public ConsoleLogRecordExporter(ConsoleExporterOptions options)
            : base(options)
        {
        }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            var rightPaddingLength = 30;
            foreach (var logRecord in batch)
            {
                this.WriteLine($"{"LogRecord.TraceId:".PadRight(rightPaddingLength)}{logRecord.TraceId}");
                this.WriteLine($"{"LogRecord.SpanId:".PadRight(rightPaddingLength)}{logRecord.SpanId}");
                this.WriteLine($"{"LogRecord.Timestamp:".PadRight(rightPaddingLength)}{logRecord.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
                this.WriteLine($"{"LogRecord.EventId:".PadRight(rightPaddingLength)}{logRecord.EventId}");
                this.WriteLine($"{"LogRecord.CategoryName:".PadRight(rightPaddingLength)}{logRecord.CategoryName}");
                this.WriteLine($"{"LogRecord.LogLevel:".PadRight(rightPaddingLength)}{logRecord.LogLevel}");
                this.WriteLine($"{"LogRecord.TraceFlags:".PadRight(rightPaddingLength)}{logRecord.TraceFlags}");
                this.WriteLine($"{"LogRecord.State:".PadRight(rightPaddingLength)}{logRecord.State}");
                if (logRecord.Exception is { })
                {
                    this.WriteLine($"{"LogRecord.Exception:".PadRight(rightPaddingLength)}{logRecord.Exception?.Message}");
                }

                this.WriteLine(string.Empty);
            }

            return ExportResult.Success;
        }
    }
}
#endif
