// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsTextFormatterTests
{
    [Fact]
    public void Format_EventIdWithName_RendersSingleBraces()
    {
        var entry = CreateEntry(LogLevel.Warning, new EventId(5, "ExporterFailed"), "message");

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains("{EventId: 5, EventName: ExporterFailed}", line, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", line, StringComparison.Ordinal);
        Assert.DoesNotContain("}}", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_EventIdZeroWithName_IsRendered()
    {
        var entry = CreateEntry(LogLevel.Warning, new EventId(0, "NamedZero"), "message");

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains("{EventId: 0, EventName: NamedZero}", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_DefaultEventId_IsOmitted()
    {
        var entry = CreateEntry(LogLevel.Warning, default, "message");

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.DoesNotContain("EventId", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_EntryWithException_KeepsOriginalLevelAndAppendsException()
    {
        var exception = new InvalidOperationException("boom");
        var entry = CreateEntry(LogLevel.Information, default, "handled failure", exception);

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains("[Information]", line, StringComparison.Ordinal);
        Assert.DoesNotContain("[Error]", line, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", line, StringComparison.Ordinal);
        Assert.Contains("boom", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesEntryTimestamp_NotFormatTime()
    {
        var timestamp = new DateTime(2020, 1, 2, 3, 4, 5, 678, DateTimeKind.Utc);
        var entry = new SelfDiagnosticsLogEntry(timestamp, 42, LogLevel.Warning, default, "message", null, null);

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.StartsWith($"[{timestamp.ToString("O", CultureInfo.InvariantCulture)}]", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Format_NonPositiveThreadId_RendersDashes(long threadId)
    {
        var entry = new SelfDiagnosticsLogEntry(DateTime.UtcNow, threadId, LogLevel.Warning, default, "message", null, null);

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains("[------]", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ThreadId_ZeroPaddedToSixChars()
    {
        var entry = new SelfDiagnosticsLogEntry(DateTime.UtcNow, 42, LogLevel.Warning, default, "message", null, null);

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains("[000042]", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NoActivity_RendersSpanDashesAndNoTraceContextSuffix()
    {
        var entry = CreateEntry(LogLevel.Warning, default, "message");

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains("[------]", line, StringComparison.Ordinal);
        Assert.DoesNotContain("<00-", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_WithActivityContext_RendersTraceContextSuffix()
    {
        using var activity = new Activity("test");
        activity.Start();

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "message", null);

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains($"<00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ActivityTraceFlags.None, "00")]
    [InlineData(ActivityTraceFlags.Recorded, "01")]
    //// https://github.com/open-telemetry/opentelemetry-dotnet/pull/6899
    //// will change this to use ActivityTraceFlags.RandomTraceId instead.
    [InlineData((ActivityTraceFlags)2, "02")]
    [InlineData(ActivityTraceFlags.Recorded | (ActivityTraceFlags)2, "03")]
    public void Format_WithActivityContext_PreservesAllTraceFlags(ActivityTraceFlags flags, string expectedFlags)
    {
        var context = new ActivityContext(
            ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c"),
            ActivitySpanId.CreateFromString("b9c7c989f97918e1"),
            flags);
        var entry = new SelfDiagnosticsLogEntry(
            DateTime.UtcNow,
            42,
            LogLevel.Warning,
            default,
            "message",
            null,
            context);

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains(
            $"<00-{context.TraceId.ToHexString()}-{context.SpanId.ToHexString()}-{expectedFlags}>",
            line,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ThreadIdExceedsSixDigits_TruncatesToLastSixChars()
    {
        var entry = new SelfDiagnosticsLogEntry(DateTime.UtcNow, 1_234_567, LogLevel.Warning, default, "message", null, null);

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains("[234567]", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "Trace")]
    [InlineData(LogLevel.Debug, "Debug")]
    [InlineData(LogLevel.Critical, "Critical")]
    [InlineData((LogLevel)99, "None")]
    public void Format_LogLevel_RendersExpectedShortString(LogLevel level, string expected)
    {
        var entry = CreateEntry(level, default, "message");

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains($"[{expected}]", line, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void Format_MessageWithLineBreak_IndentsContinuationLines(string lineBreak)
    {
        const string Lookalike = "[2026-01-01T00:00:00.0000000Z][000001][------][Critical]    OpenTelemetry-Sdk: lookalike";
        var entry = CreateEntry(LogLevel.Error, new EventId(17, "HttpRequestFailed"), $"Response: rejected{lineBreak}{Lookalike}{lineBreak}. Exception: none");

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        var lines = text.Split([Environment.NewLine], StringSplitOptions.None);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("[", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("Response: rejected", lines[0], StringComparison.Ordinal);
        Assert.Equal($"    {Lookalike}", lines[1]);
        Assert.Equal("    . Exception: none {EventId: 17, EventName: HttpRequestFailed}", lines[2]);
    }

    [Fact]
    public void Format_EntryWithException_IndentsExceptionLines()
    {
        const string Lookalike = "[2026-01-01T00:00:00.0000000Z][000001][------][Critical]    OpenTelemetry-Sdk: lookalike";
        var exception = new InvalidOperationException($"boom\n{Lookalike}");
        var entry = CreateEntry(LogLevel.Error, default, "handled failure", exception);

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        var lines = text.Split([Environment.NewLine], StringSplitOptions.None);

        Assert.Equal(3, lines.Length);
        Assert.EndsWith("handled failure", lines[0], StringComparison.Ordinal);
        Assert.Equal("    System.InvalidOperationException: boom", lines[1]);
        Assert.Equal($"    {Lookalike}", lines[2]);
    }

    [Fact]
    public void Format_EventNameWithLineBreak_IndentsContinuationLines()
    {
        var entry = CreateEntry(LogLevel.Warning, new EventId(1, "Name\n[line]"), "message");

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.EndsWith($"{{EventId: 1, EventName: Name{Environment.NewLine}    [line]}}", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData('\0', "\\u0000")]
    [InlineData('\b', "\\u0008")]
    [InlineData('\v', "\\u000B")]
    [InlineData('\f', "\\u000C")]
    [InlineData('\u001b', "\\u001B")]
    [InlineData('\u007f', "\\u007F")]
    [InlineData('\u0085', "\\u0085")]
    [InlineData('\u2028', "\\u2028")]
    [InlineData('\u2029', "\\u2029")]
    public void Format_MessageWithControlCharacter_EscapesIt(char control, string expected)
    {
        var entry = CreateEntry(LogLevel.Warning, default, $"before{control}after");

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.EndsWith($"before{expected}after", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ExceptionWithLineBreakAndControlCharacter_IndentsAndEscapesIt()
    {
        var exception = new InvalidOperationException("one\r\ntwo\u001bthree");
        var entry = CreateEntry(LogLevel.Error, default, "handled failure", exception);

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        var lines = text.Split([Environment.NewLine], StringSplitOptions.None);

        Assert.Equal(3, lines.Length);
        Assert.Equal("    System.InvalidOperationException: one", lines[1]);
        Assert.StartsWith("    two\\u001Bthree", lines[2], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("first\n\rsecond", "first||    second")]
    [InlineData("first\n\nsecond", "first||    second")]
    [InlineData("first\r\n\r\nsecond", "first||    second")]
    [InlineData("first\n\n\nsecond", "first|||    second")]
    public void Format_MessageWithConsecutiveLineBreaks_LeavesEmptyLinesEmpty(string message, string expected)
    {
        var entry = CreateEntry(LogLevel.Warning, default, message);

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        var lines = text.Split([Environment.NewLine], StringSplitOptions.None);

        // The first line also holds the entry prefix, so compare the end of the joined lines.
        Assert.EndsWith(expected, string.Join("|", lines), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("message\n")]
    [InlineData("message\r\n")]
    [InlineData("message\r")]
    [InlineData("message\n\n\n")]
    public void Format_MessageWithTrailingLineBreak_DropsIt(string message)
    {
        var entry = CreateEntry(LogLevel.Warning, new EventId(17, "HttpRequestFailed"), message);

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.DoesNotContain(Environment.NewLine, text, StringComparison.Ordinal);
        Assert.EndsWith("message {EventId: 17, EventName: HttpRequestFailed}", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n\r\n")]
    public void Format_MessageWithNoText_WritesNoText(string message)
    {
        var entry = CreateEntry(LogLevel.Warning, default, message);

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.DoesNotContain(Environment.NewLine, text, StringComparison.Ordinal);
        Assert.EndsWith("[Warning]", text.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_MessageWithTab_IsPreserved()
    {
        var entry = CreateEntry(LogLevel.Warning, default, "before\tafter");

        var text = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.EndsWith("before\tafter", text, StringComparison.Ordinal);
    }

    private static SelfDiagnosticsLogEntry CreateEntry(
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception = null)
        => new(DateTime.UtcNow, 1, level, eventId, message, exception, null);
}
