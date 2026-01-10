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
/// "[Timestamp] [SeverityRange] [EventName] [TraceId][-SpanId]]: [Body]".
/// </remarks>
internal sealed class CompactLogRecordFormatter : CompactFormatterBase<LogRecord>
{
    private const ConsoleColor MessageForeground = ConsoleColor.White;
    private const ConsoleColor MessageBackground = ConsoleColor.Black;

    private static readonly string ExceptionIndent = new string(' ', 2);
    private static readonly char[] ExceptionSplitChars = new[] { '\r', '\n' };
    private static readonly PropertyInfo? SeverityProperty = typeof(LogRecord).GetProperty(
        "Severity",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    public CompactLogRecordFormatter(ConsoleExporterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> batch, ConsoleFormatterContext context)
    {
        var console = this.Options.Console;

        foreach (var logRecord in batch)
        {
            // Build the timestamp (if configured)
            var timestamp = string.Empty;
            if (!string.IsNullOrEmpty(this.Options.TimestampFormat))
            {
                var timestampToFormat = this.Options.UseUtcTimestamp
                    ? logRecord.Timestamp.ToUniversalTime()
                    : logRecord.Timestamp.ToLocalTime();

                timestamp = timestampToFormat.ToString(
                    this.Options.TimestampFormat!,
                    CultureInfo.InvariantCulture);
            }

            // Get the severity
            var severity = GetSeverityString(logRecord);

            // Build the details
            var logDetails = string.Empty;

            // If this is an Event, then include the Event name (or numeric reference)
            if (!string.IsNullOrEmpty(logRecord.EventId.Name))
            {
                logDetails += $" [{logRecord.EventId.Name}]";
            }
            else if (logRecord.EventId.Id != 0)
            {
                logDetails += $" [{logRecord.EventId.Id}]";
            }

            // Output trace ID & span ID
            if (logRecord.TraceId != default)
            {
                var traceIdHex = logRecord.TraceId.ToHexString();
                logDetails += $" {traceIdHex}";

                if (logRecord.SpanId != default)
                {
                    logDetails += $"-{logRecord.SpanId.ToHexString()}";
                }
            }

            // Build the message. Use FormattedMessage if available, otherwise fall back to Body
            var message = !string.IsNullOrEmpty(logRecord.FormattedMessage)
                ? logRecord.FormattedMessage
                : logRecord.Body?.ToString() ?? string.Empty;

            lock (console.SyncRoot)
            {
                // Output the line, starting with timestamp (if specified)
                console.Write(timestamp);

                // Write severity in color, then rest of the line in default color
                var severityColors = GetSeverityColors(severity);
                console.WriteColor(severity, severityColors.Foreground, severityColors.Background);

                console.Write(logDetails);

                if (!string.IsNullOrWhiteSpace(message))
                {
                    console.Write(": ");
                    console.WriteColor(message!, MessageForeground, MessageBackground);
                }

                console.WriteLine(string.Empty);

                // Output exception details if present, indented
                if (logRecord.Exception != null)
                {
                    var exceptionLines = logRecord
                        .Exception.ToString()
                        .Split(ExceptionSplitChars, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in exceptionLines)
                    {
                        console.WriteLine($"{ExceptionIndent}{line}");
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

    private static (ConsoleColor Foreground, ConsoleColor Background) GetSeverityColors(string severity)
    {
        switch (severity)
        {
            case "TRACE":
            case "DEBUG":
                return (ConsoleColor.Gray, ConsoleColor.Black);
            case "INFO":
                return (ConsoleColor.DarkGreen, ConsoleColor.Black);
            case "WARN":
                return (ConsoleColor.Yellow, ConsoleColor.Black);
            case "ERROR":
                return (ConsoleColor.Black, ConsoleColor.DarkRed);
            case "FATAL":
                return (ConsoleColor.White, ConsoleColor.DarkRed);
            default:
                return (ConsoleColor.Gray, ConsoleColor.Black);
        }
    }
}
