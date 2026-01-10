// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Reflection;
using OpenTelemetry.Logs;

// using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Formatting.Compact;

/// <summary>
/// Simple console exporter for OpenTelemetry logs.
/// </summary>
/// <remarks>
/// Default format is:
/// "[Timestamp] [SeverityRange] [EventName] [TraceId][-SpanId]]
///  [Body]".
/// </remarks>
internal sealed class CompactLogRecordFormatter : CompactFormatterBase<LogRecord>
{
    private static readonly char[] ExceptionSplitChars = new[] { '\r', '\n' };
    private static readonly string Indent = new string(' ', 9);
    private static readonly PropertyInfo? SeverityProperty = typeof(LogRecord).GetProperty(
        "Severity",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    private readonly object syncLock = new();
    private readonly CompactFormatterOptions formatterOptions;

    public CompactLogRecordFormatter(ConsoleExporterOptions exporterOptions, CompactFormatterOptions formatterOptions)
        : base(exporterOptions, formatterOptions)
    {
        this.formatterOptions = formatterOptions ?? throw new ArgumentNullException(nameof(formatterOptions));
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> batch, ConsoleFormatterContext context)
    {
        var console = this.formatterOptions.Console;

        foreach (var logRecord in batch)
        {
            lock (this.syncLock)
            {
                // Build the timestamp (if configured)
                var timestamp = string.Empty;
                if (!string.IsNullOrEmpty(this.formatterOptions.TimestampFormat))
                {
                    var timestampToFormat = this.formatterOptions.UseUtcTimestamp
                        ? logRecord.Timestamp.ToUniversalTime()
                        : logRecord.Timestamp.ToLocalTime();

                    timestamp = timestampToFormat.ToString(
                        this.formatterOptions.TimestampFormat!,
                        CultureInfo.InvariantCulture);
                }

                // Get the severity
                var severity = GetSeverityString(logRecord);

                // Build the first line details
                var firstLine = string.Empty;

                // If this is an Event, then include the Event name (or numeric reference)
                if (!string.IsNullOrEmpty(logRecord.EventId.Name))
                {
                    firstLine += $" [{logRecord.EventId.Name}]";
                }
                else if (logRecord.EventId.Id != 0)
                {
                    firstLine += $" [{logRecord.EventId.Id}]";
                }

                // Output trace ID & span ID
                if (logRecord.TraceId != default)
                {
                    var traceIdHex = logRecord.TraceId.ToHexString();
                    firstLine += $" {traceIdHex}";

                    if (logRecord.SpanId != default)
                    {
                        firstLine += $"-{logRecord.SpanId.ToHexString()}";
                    }
                }

                // var resource = context.GetResource();
                // if (resource != Resource.Empty)
                // {
                //     var serviceName = default(string);
                //     foreach (var resourceAttribute in resource.Attributes)
                //     {
                //         if (resourceAttribute.Key == "service.name")
                //         {
                //             serviceName = resourceAttribute.Value.ToString();
                //             break;
                //         }
                //     }

                // if (!string.IsNullOrEmpty(serviceName))
                //     {
                //         firstLine += $" {serviceName}";
                //     }
                // }

                // Dotnet legacy support of Category
                // if (!string.IsNullOrEmpty(logRecord.CategoryName))
                // {
                //     firstLine += $" ({logRecord.CategoryName})";
                // }

                // Build the message. Use FormattedMessage if available, otherwise fall back to Body
                var message = !string.IsNullOrEmpty(logRecord.FormattedMessage)
                    ? logRecord.FormattedMessage
                    : logRecord.Body?.ToString() ?? string.Empty;

                // Output the line, starting with timestamp (if specified)
                console.Write(timestamp);

                // Write severity in color, then rest of the line in default color
                var originalForeground = console.ForegroundColor;
                var originalBackground = console.BackgroundColor;
                SetSeverityColors(severity, console);
                console.Write(severity);
                console.ForegroundColor = originalForeground;
                console.BackgroundColor = originalBackground;

                console.WriteLine(firstLine);

                console.ForegroundColor = ConsoleColor.White;
                console.BackgroundColor = ConsoleColor.Black;
                console.WriteLine($"{Indent}{message}");
                console.ForegroundColor = originalForeground;
                console.BackgroundColor = originalBackground;

                // Output exception details if present, indented
                if (logRecord.Exception != null)
                {
                    var exceptionLines = logRecord
                        .Exception.ToString()
                        .Split(ExceptionSplitChars, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in exceptionLines)
                    {
                        console.WriteLine($"{Indent}{line}");
                    }
                }
            }
        }

        return ExportResult.Success;
    }

    /// <summary>
    /// Gets the severity string for a log record.
    /// </summary>
    /// <description>
    /// This method uses reflection to access the internal Severity property if available.
    /// When LogRecord.Severity becomes public, update this method to use it directly.
    /// </description>
    private static string GetSeverityString(LogRecord logRecord)
    {
        // Get the internal Severity property via reflection
        var severityValue = SeverityProperty?.GetValue(logRecord);
        if (severityValue != null)
        {
            // Convert LogRecordSeverity enum to int
            int sevNum = (int)severityValue;

            // OpenTelemetry log severity number ranges:
            // 1-4: Trace, 5-8: Debug, 9-12: Info, 13-16: Warn, 17-20: Error, 21-24: Critical
            return sevNum switch
            {
                >= 1 and <= 4 => "TRACE",
                >= 5 and <= 8 => "DEBUG",
                >= 9 and <= 12 => "INFO",
                >= 13 and <= 16 => "WARN",
                >= 17 and <= 20 => "ERROR",
                >= 21 and <= 24 => "FATAL",
                _ => $"[{sevNum}]",
            };
        }

        return "UNSPEC";
    }

    private static void SetSeverityColors(string severity, IConsole console)
    {
        switch (severity)
        {
            case "TRACE":
            case "DEBUG":
                console.ForegroundColor = ConsoleColor.Gray;
                console.BackgroundColor = ConsoleColor.Black;
                break;
            case "INFO":
                console.ForegroundColor = ConsoleColor.DarkGreen;
                console.BackgroundColor = ConsoleColor.Black;
                break;
            case "WARN":
                console.ForegroundColor = ConsoleColor.Yellow;
                console.BackgroundColor = ConsoleColor.Black;
                break;
            case "ERROR":
                console.ForegroundColor = ConsoleColor.Black;
                console.BackgroundColor = ConsoleColor.DarkRed;
                break;
            case "FATAL":
                console.ForegroundColor = ConsoleColor.White;
                console.BackgroundColor = ConsoleColor.DarkRed;
                break;
            default:
                console.ForegroundColor = ConsoleColor.Gray;
                console.BackgroundColor = ConsoleColor.Black;
                break;
        }
    }
}
