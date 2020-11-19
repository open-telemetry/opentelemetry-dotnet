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

namespace OpenTelemetry.Exporter.Console
{
    internal class ConsoleLogRecordExporter : ConsoleExporter<LogRecord>
    {
        public ConsoleLogRecordExporter(ConsoleExporterOptions options)
            : base(options)
        {
            this.Init((logRecord, writeTo) => this.ExportLogRecord(logRecord, writeTo));
        }

        private void ExportLogRecord(LogRecord logRecord, Action<string> writeLine)
        {
            var rightPaddingLength = 30;
            writeLine($"{"LogRecord.TraceId:".PadRight(rightPaddingLength)}{logRecord.TraceId}");
            writeLine($"{"LogRecord.SpanId:".PadRight(rightPaddingLength)}{logRecord.SpanId}");
            writeLine($"{"LogRecord.Timestamp:".PadRight(rightPaddingLength)}{logRecord.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ}");
            writeLine($"{"LogRecord.EventId:".PadRight(rightPaddingLength)}{logRecord.EventId}");
            writeLine($"{"LogRecord.CategoryName:".PadRight(rightPaddingLength)}{logRecord.CategoryName}");
            writeLine($"{"LogRecord.LogLevel:".PadRight(rightPaddingLength)}{logRecord.LogLevel}");
            writeLine($"{"LogRecord.TraceFlags:".PadRight(rightPaddingLength)}{logRecord.TraceFlags}");
            writeLine($"{"LogRecord.State:".PadRight(rightPaddingLength)}{logRecord.State}");
            if (logRecord.Exception is { })
            {
                writeLine($"{"LogRecord.Exception:".PadRight(rightPaddingLength)}{logRecord.Exception?.Message}");
            }

            writeLine(string.Empty);
        }
    }
}
#endif
