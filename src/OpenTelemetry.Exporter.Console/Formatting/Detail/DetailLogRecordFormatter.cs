// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Formatting.Detail;

internal sealed class DetailLogRecordFormatter : DetailFormatterBase<LogRecord>
{
    private const int RightPaddingLength = 35;
    private readonly Lock syncObject = new();
    private bool disposed;
    private string? disposedStackTrace;
    private bool isDisposeMessageSent;

    public DetailLogRecordFormatter(ConsoleExporterOptions options)
        : base(options)
    {
    }

    public override ExportResult Export(in Batch<LogRecord> batch, ConsoleFormatterContext context)
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
                this.WriteLine($"{Environment.NewLine}Dispose was called on the following stack trace:");
                this.WriteLine(this.disposedStackTrace!);
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

            if (logRecord.Severity.HasValue)
            {
                this.WriteLine($"{"LogRecord.Severity:",-RightPaddingLength}{logRecord.Severity}");
            }

            if (logRecord.SeverityText != null)
            {
                this.WriteLine($"{"LogRecord.SeverityText:",-RightPaddingLength}{logRecord.SeverityText}");
            }

            if (logRecord.FormattedMessage != null)
            {
                this.WriteLine($"{"LogRecord.FormattedMessage:",-RightPaddingLength}{logRecord.FormattedMessage}");
            }

            if (logRecord.Body != null)
            {
                this.WriteLine($"{"LogRecord.Body:",-RightPaddingLength}{logRecord.Body}");
            }

            if (logRecord.Attributes != null)
            {
                this.WriteLine("LogRecord.Attributes (Key:Value):");
                for (int i = 0; i < logRecord.Attributes.Count; i++)
                {
                    // Special casing {OriginalFormat}
                    // See https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182
                    // for explanation.
                    var valueToTransform = logRecord.Attributes[i].Key.Equals("{OriginalFormat}", StringComparison.Ordinal)
                        ? new KeyValuePair<string, object?>("OriginalFormat (a.k.a Body)", logRecord.Attributes[i].Value)
                        : logRecord.Attributes[i];

                    if (this.TagWriter.TryTransformTag(valueToTransform, out var result))
                    {
                        this.WriteLine($"{string.Empty,-4}{result.Key}: {result.Value}");
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
                this.WriteLine($"{"LogRecord.Exception:",-RightPaddingLength}{logRecord.Exception.ToInvariantString()}");
            }

            int scopeDepth = -1;

            logRecord.ForEachScope(ProcessScope, this);

            void ProcessScope(LogRecordScope scope, DetailLogRecordFormatter formatter)
            {
                if (++scopeDepth == 0)
                {
                    formatter.WriteLine("LogRecord.ScopeValues (Key:Value):");
                }

                foreach (KeyValuePair<string, object?> scopeItem in scope)
                {
                    if (this.TagWriter.TryTransformTag(scopeItem, out var result))
                    {
                        formatter.WriteLine($"[Scope.{scopeDepth}]:{result.Key}: {result.Value}");
                    }
                }
            }

            var resource = context.GetResource();
            if (resource != Resource.Empty)
            {
                this.WriteLine($"{Environment.NewLine}Resource associated with LogRecord:");
                foreach (var resourceAttribute in resource.Attributes)
                {
                    if (this.TagWriter.TryTransformTag(resourceAttribute.Key, resourceAttribute.Value, out var result))
                    {
                        this.WriteLine($"{result.Key}: {result.Value}");
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
