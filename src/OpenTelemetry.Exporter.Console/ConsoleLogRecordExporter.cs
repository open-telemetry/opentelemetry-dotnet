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

using System.Collections.Generic;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter
{
    public class ConsoleLogRecordExporter : ConsoleExporter<LogRecord>
    {
        private const int RightPaddingLength = 35;

        public ConsoleLogRecordExporter(ConsoleExporterOptions options)
            : base(options)
        {
        }

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var logRecord in batch)
            {
                this.WriteLine($"{"LogRecord.Timestamp:",-RightPaddingLength}{logRecord.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ}");

                if (logRecord.TraceId != default)
                {
                    this.WriteLine($"{"LogRecord.TraceId:",-RightPaddingLength}{logRecord.TraceId}");
                    this.WriteLine($"{"LogRecord.SpanId:",-RightPaddingLength}{logRecord.SpanId}");
                    this.WriteLine($"{"LogRecord.TraceFlags:",-RightPaddingLength}{logRecord.TraceFlags}");
                }

                this.WriteLine($"{"LogRecord.CategoryName:",-RightPaddingLength}{logRecord.CategoryName}");
                this.WriteLine($"{"LogRecord.LogLevel:",-RightPaddingLength}{logRecord.LogLevel}");
                if (logRecord.FormattedMessage != null)
                {
                    this.WriteLine($"{"LogRecord.FormattedMessage:",-RightPaddingLength}{logRecord.FormattedMessage}");
                }

                if (logRecord.State != null)
                {
                    this.WriteLine($"{"LogRecord.State:",-RightPaddingLength}{logRecord.State}");
                }
                else if (logRecord.StateValues != null)
                {
                    this.WriteLine("LogRecord.StateValues (Key:Value):");
                    for (int i = 0; i < logRecord.StateValues.Count; i++)
                    {
                        this.WriteLine($"{logRecord.StateValues[i].Key,-RightPaddingLength}{logRecord.StateValues[i].Value}");
                    }
                }

                if (logRecord.EventId != default)
                {
                    this.WriteLine($"{"LogRecord.EventId:",-RightPaddingLength}{logRecord.EventId.Id}");
                    if (string.IsNullOrEmpty(logRecord.EventId.Name))
                    {
                        this.WriteLine($"{"LogRecord.EventName:",-RightPaddingLength}{logRecord.EventId.Name}");
                    }
                }

                if (logRecord.Exception is { })
                {
                    this.WriteLine($"{"LogRecord.Exception:",-RightPaddingLength}{logRecord.Exception?.Message}");
                }

                int scopeDepth = -1;

                logRecord.ForEachScope(ProcessScope, this);

                void ProcessScope(LogRecordScope scope, ConsoleLogRecordExporter exporter)
                {
                    if (++scopeDepth == 0)
                    {
                        exporter.WriteLine("LogRecord.ScopeValues (Key:Value):");
                    }

                    foreach (KeyValuePair<string, object> scopeItem in scope)
                    {
                        exporter.WriteLine($"[Scope.{scopeDepth}]:{scopeItem.Key,-RightPaddingLength}{scopeItem.Value}");
                    }
                }

                var resource = this.ParentProvider.GetResource();
                if (resource != Resource.Empty)
                {
                    this.WriteLine("\nResource associated with LogRecord:");
                    foreach (var resourceAttribute in resource.Attributes)
                    {
                        this.WriteLine($"{resourceAttribute.Key}: {resourceAttribute.Value}");
                    }
                }

                this.WriteLine(string.Empty);
            }

            return ExportResult.Success;
        }
    }
}
