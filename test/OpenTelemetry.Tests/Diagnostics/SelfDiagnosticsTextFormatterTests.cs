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

    private static SelfDiagnosticsLogEntry CreateEntry(
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception = null)
        => new(DateTime.UtcNow, 1, level, eventId, message, exception, null);
}
