// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal;

/// <summary>
/// The default text formatter for self-diagnostics entries:
/// <code>[timestamp:O][thread 6-char][spanId 6-char][level]{padded to 60} message {EventId: n} &lt;traceId-spanId-flags&gt;</code>
/// followed by <see cref="Exception.ToString"/> on subsequent lines when an exception is present.
/// </summary>
internal sealed class SelfDiagnosticsTextFormatter : ISelfDiagnosticsFormatter
{
    /// <summary>
    /// The shared instance. Sinks sharing this reference share one format per entry.
    /// </summary>
    internal static readonly SelfDiagnosticsTextFormatter Instance = new();

    private const string EmptySpanId = "------";
    private const int PrefixPadLength = 60;

    private SelfDiagnosticsTextFormatter()
    {
    }

    /// <inheritdoc/>
    public string? FileHeader => "DateTime (UTC)                Thread  SpanId  Level         Message";

    /// <inheritdoc/>
    public string Format(in SelfDiagnosticsLogEntry entry)
    {
        var builder = StringBuilderCache.Acquire();

        WritePrefix(entry.TimestampUtc, entry.ThreadId, entry.Level, entry.ActivityContext?.SpanId.ToHexString(), builder);

        builder.Append(entry.Message);

        // EventId equality compares Id only, so test Id and Name independently to avoid
        // suppressing EventId(0, "SomeName").
        if (entry.EventId.Id != 0 || !string.IsNullOrEmpty(entry.EventId.Name))
        {
            builder.Append(" {EventId: ").Append(entry.EventId.Id);
            if (!string.IsNullOrEmpty(entry.EventId.Name))
            {
                builder.Append(", EventName: ").Append(entry.EventId.Name);
            }

            builder.Append('}');
        }

        if (entry.ActivityContext.HasValue)
        {
            var ctx = entry.ActivityContext.Value;
            builder.Append(" <00-")
                   .Append(ctx.TraceId.ToHexString())
                   .Append('-')
                   .Append(ctx.SpanId.ToHexString())
                   .Append('-')
                   .Append(ctx.TraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00")
                   .Append('>');
        }

        if (entry.Exception is not null)
        {
            builder.Append(Environment.NewLine).Append(entry.Exception);
        }

        return StringBuilderCache.GetStringAndRelease(builder);
    }

    private static void WritePrefix(
        DateTime timestampUtc,
        long threadId,
        LogLevel level,
        string? spanIdHex,
        StringBuilder builder)
    {
        const int maxLen = 6;

        // Thread ID: right-aligned, zero-padded to 6 chars; dashes when unavailable.
        var threadStr = threadId <= 0
            ? new string('-', maxLen)
            : threadId.ToString("D", CultureInfo.InvariantCulture).PadLeft(maxLen, '0');

        if (threadStr.Length > maxLen)
        {
            threadStr = threadStr.Substring(threadStr.Length - maxLen);
        }

        // Span ID: first 6 hex chars or dashes.
        var spanStr = string.IsNullOrEmpty(spanIdHex)
            ? EmptySpanId
            : spanIdHex!.Length >= maxLen ? spanIdHex.Substring(0, maxLen) : spanIdHex.PadRight(maxLen, '-');

        builder
            .Append('[').Append(timestampUtc.ToString("O", CultureInfo.InvariantCulture)).Append(']')
            .Append('[').Append(threadStr).Append(']')
            .Append('[').Append(spanStr).Append(']')
            .Append('[').Append(LogLevelToShortString(level)).Append(']');

        var padding = PrefixPadLength - builder.Length;
        if (padding > 0)
        {
            builder.Append(' ', padding);
        }
    }

    private static string LogLevelToShortString(LogLevel level) => level switch
    {
        LogLevel.Trace => "Trace",
        LogLevel.Debug => "Debug",
        LogLevel.Information => "Information",
        LogLevel.Warning => "Warning",
        LogLevel.Error => "Error",
        LogLevel.Critical => "Critical",
        _ => "None",
    };
}
