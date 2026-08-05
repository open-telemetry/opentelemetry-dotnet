// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Diagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsTextFormatterTests
{
    [Fact]
    public void Format_EventIdWithName_RendersSingleBraces()
    {
        // Regression: the original implementation appended "{{"/"}}" literally (a copy/paste
        // from composite-format escaping), producing doubled braces in the output.
        var entry = CreateEntry(LogLevel.Warning, new EventId(5, "ExporterFailed"), "message");

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains("{EventId: 5, EventName: ExporterFailed}", line, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", line, StringComparison.Ordinal);
        Assert.DoesNotContain("}}", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_EventIdZeroWithName_IsRendered()
    {
        // Regression: EventId equality compares Id only, so "eventId != default" wrongly
        // suppressed EventId(0, "SomeName").
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
        // Regression: the original implementation escalated the rendered level to Error while
        // filtering/routing still used the original level, making the output disagree with
        // where it was routed.
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
        // Regression: entries buffered before activation must render with their capture-time
        // timestamp, not the time at which the pump eventually formats them.
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
        using var activity = new System.Diagnostics.Activity("test");
        activity.Start();

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "message", null);

        var line = SelfDiagnosticsTextFormatter.Instance.Format(in entry);

        Assert.Contains($"<00-{activity.TraceId.ToHexString()}-{activity.SpanId.ToHexString()}-", line, StringComparison.Ordinal);
    }

    private static SelfDiagnosticsLogEntry CreateEntry(
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception = null)
        => new(DateTime.UtcNow, 1, level, eventId, message, exception, null);
}
