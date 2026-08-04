// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Diagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsLoggingEventListenerTests
{
    private const string SelfDescribingSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-SelfDescribing";

    [Fact]
    public void SelfDescribingEvent_RendersPayloadNamesAndValues()
    {
        // Regression: sources that use EventSource.Write (TraceLogging) carry a null Message and
        // put everything in the payload. The listener used to emit only "SourceName: " for them,
        // silently discarding the event body.
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static () => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));

        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        using var eventSource = new EventSource(
            SelfDescribingSourceName,
            EventSourceSettings.EtwSelfDescribingEventFormat);

        eventSource.Write(
            "CustomEvent",
            new EventSourceOptions { Level = EventLevel.Warning },
            new SelfDescribingPayload { Detail = "payload value", Attempt = 3 });

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => sink.Written.Any(item => item.Entry.Message.StartsWith(SelfDescribingSourceName, StringComparison.Ordinal))));

        var message = sink.Written
            .First(item => item.Entry.Message.StartsWith(SelfDescribingSourceName, StringComparison.Ordinal))
            .Entry.Message;

        Assert.Contains("Detail=payload value", message, StringComparison.Ordinal);
        Assert.Contains("Attempt=3", message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventLevelBelowSubscription_IsNotDelivered()
    {
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static () => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        Assert.True(dispatcher.Activate([sink], LogLevel.Error));

        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Error);
        using var eventSource = new LevelTestEventSource();

        eventSource.WarningEvent("below the subscription level");
        eventSource.ErrorEvent("at the subscription level");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => sink.Written.Any(item => item.Entry.EventId.Id == 2)));
        Assert.DoesNotContain(sink.Written, item => item.Entry.EventId.Id == 1);
    }

    [EventData]
    private struct SelfDescribingPayload
    {
        public string Detail { get; set; }

        public int Attempt { get; set; }
    }

    [EventSource(Name = "OpenTelemetry-SelfDiagnosticsListenerTests-Levels")]
    private sealed class LevelTestEventSource : EventSource
    {
        [Event(1, Level = EventLevel.Warning, Message = "{0}")]
        public void WarningEvent(string message) => this.WriteEvent(1, message);

        [Event(2, Level = EventLevel.Error, Message = "{0}")]
        public void ErrorEvent(string message) => this.WriteEvent(2, message);
    }
}
