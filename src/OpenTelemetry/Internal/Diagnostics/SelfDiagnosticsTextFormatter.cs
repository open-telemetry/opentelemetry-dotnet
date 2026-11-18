// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal;

/// <summary>
/// The default text formatter for self-diagnostics entries:
/// <code>[timestamp:O][thread 6-char][spanId 6-char][level]{padded to 60} message {EventId: n} &lt;traceId-spanId-flags&gt;</code>
/// followed by <see cref="Exception.ToString"/> on subsequent lines when an exception is present.
/// </summary>
/// <remarks>
/// The text of an entry is not guaranteed to be a single line or to be free of control characters,
/// as event payloads can contain text received from a remote peer. Every line of an entry after
/// its first is indented and other control characters are escaped, so that the text of an entry
/// cannot itself start a line with an entry prefix.
/// <para/>
/// This applies only to entries formatted here. Other text written to the same sinks, such as the
/// preamble of a log file or a sink's own error messages, does not pass through this formatter.
/// </remarks>
internal sealed class SelfDiagnosticsTextFormatter : ISelfDiagnosticsFormatter
{
    /// <summary>
    /// The shared instance. Sinks sharing this reference share one format per entry.
    /// </summary>
    internal static readonly SelfDiagnosticsTextFormatter Instance = new();

    private const string EmptySpanId = "------";
    private const int PrefixPadLength = 60;
    private const string ContinuationIndent = "    ";

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

        AppendText(entry.Message, builder);

        // EventId equality compares Id only, so test Id and Name independently to avoid
        // suppressing EventId(0, "SomeName").
        if (entry.EventId.Id != 0 || !string.IsNullOrEmpty(entry.EventId.Name))
        {
            builder.Append(" {EventId: ").Append(entry.EventId.Id);
            if (!string.IsNullOrEmpty(entry.EventId.Name))
            {
                builder.Append(", EventName: ");
                AppendText(entry.EventId.Name, builder);
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
                   .Append(((byte)ctx.TraceFlags).ToString("x2", CultureInfo.InvariantCulture))
                   .Append('>');
        }

        if (entry.Exception is not null)
        {
            builder.Append(Environment.NewLine).Append(ContinuationIndent);
            AppendText(entry.Exception.ToString(), builder);
        }

        return StringBuilderCache.GetStringAndRelease(builder);
    }

    // Printable ASCII, the overwhelming majority of text, is rejected by the first comparison pair.
    // Everything else that needs attention is a C0 control character other than tab, DEL, a C1
    // control character, or the Unicode line and paragraph separators, which are not control
    // characters but are line breaks to some readers.
    private static bool NeedsHandling(char c)
        => c < ' ' ? c != '\t' : c >= '\u007f' && (c <= '\u009f' || c == '\u2028' || c == '\u2029');

    private static void AppendText(string? text, StringBuilder builder)
    {
        if (text is null)
        {
            return;
        }

        // Line breaks at the end of the text carry no information and would leave whatever follows
        // the text on a line of its own, so they are dropped.
        var end = text.Length;
        while (end > 0 && text[end - 1] is '\r' or '\n')
        {
            end--;
        }

        var start = 0;

        for (var i = 0; i < end; i++)
        {
            var c = text[i];

            if (!NeedsHandling(c))
            {
                continue;
            }

            builder.Append(text, start, i - start);

            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < end && text[i + 1] == '\n')
                {
                    i++;
                }

                builder.Append(Environment.NewLine);

                // Indent what follows a line break so it cannot pass for the prefix of another
                // entry, but leave an empty line empty rather than writing trailing whitespace.
                if (i + 1 < end && text[i + 1] is not ('\r' or '\n'))
                {
                    builder.Append(ContinuationIndent);
                }
            }
            else
            {
                builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }

            start = i + 1;
        }

        builder.Append(text, start, end - start);
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
        var spanStr = spanIdHex is not { Length: > 0 }
            ? EmptySpanId
            : spanIdHex.Length >= maxLen ? spanIdHex.Substring(0, maxLen) : spanIdHex.PadRight(maxLen, '-');

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
        LogLevel.None or _ => "None",
    };
}
