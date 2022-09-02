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

using System;
using System.Collections.Generic;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter
{
    public class ConsoleLogRecordExporter : ConsoleExporter<LogRecord>
    {
        private const int RightPaddingLength = 35;
        private readonly object syncObject = new();
        private bool disposed;
        private string disposedStackTrace;
        private bool isDisposeMessageSent;

        public ConsoleLogRecordExporter(ConsoleExporterOptions options)
            : base(options)
        {
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
                this.WriteLine($"{"LogRecord.Timestamp:",-RightPaddingLength}{logRecord.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffffZ}");

                if (logRecord.TraceId != default)
                {
                    this.WriteLine($"{"LogRecord.TraceId:",-RightPaddingLength}{logRecord.TraceId}");
                    this.WriteLine($"{"LogRecord.SpanId:",-RightPaddingLength}{logRecord.SpanId}");
                    this.WriteLine($"{"LogRecord.TraceFlags:",-RightPaddingLength}{logRecord.TraceFlags}");
                }

                if (logRecord.CategoryName != null)
                {
                    this.WriteLine($"{"LogRecord.CategoryName:",-RightPaddingLength}{logRecord.CategoryName}");
                }

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
                        // Special casing {OriginalFormat}
                        // See https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182
                        // for explanation.
                        var valueToTransform = logRecord.StateValues[i].Key.Equals("{OriginalFormat}")
                            ? new KeyValuePair<string, object>("OriginalFormat (a.k.a Body)", logRecord.StateValues[i].Value)
                            : logRecord.StateValues[i];

                        if (ConsoleTagTransformer.Instance.TryTransformTag(valueToTransform, out var result))
                        {
                            this.WriteLine($"{string.Empty,-4}{result}");
                        }
                    }
                }

                if (logRecord.EventId != default)
                {
                    this.WriteLine($"{"LogRecord.EventId:",-RightPaddingLength}{logRecord.EventId.Id}");
                    if (!string.IsNullOrEmpty(logRecord.EventId.Name))
                    {
                        this.WriteLine($"{"LogRecord.EventName:",-RightPaddingLength}{logRecord.EventId.Name}");
                    }
                }

                if (logRecord.Exception != null)
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
                        if (ConsoleTagTransformer.Instance.TryTransformTag(scopeItem, out var result))
                        {
                            exporter.WriteLine($"[Scope.{scopeDepth}]:{result}");
                        }
                    }
                }

                var resource = this.ParentProvider.GetResource();
                if (resource != Resource.Empty)
                {
                    this.WriteLine("\nResource associated with LogRecord:");
                    foreach (var resourceAttribute in resource.Attributes)
                    {
                        if (ConsoleTagTransformer.Instance.TryTransformTag(resourceAttribute, out var result))
                        {
                            this.WriteLine(result);
                        }
                    }
                }

                this.WriteLine(string.Empty);
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
