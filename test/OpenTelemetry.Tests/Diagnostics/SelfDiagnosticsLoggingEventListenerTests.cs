// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsLoggingEventListenerTests
{
    // One EventSource name per test. Two live EventSource instances sharing a name are rejected by
    // the runtime, and OpenTelemetrySdkEventSource.Listener (a process-lifetime listener present in
    // DEBUG builds) throws out of OnEventSourceCreated on a duplicate name, which leaves the second
    // instance permanently disabled. Names must therefore never be reused across tests.
    private const string SelfDescribingSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-SelfDescribing";
    private const string SubstitutedMessageSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-Substituted";
    private const string MismatchedMessageSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-Mismatched";
    private const string MappedLevelSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-MappedLevels";
    private const string ResubscribeSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-Resubscribe";

    // Deliberately does *not* start with "OpenTelemetry-": this is the source the cross-source
    // filter has to reject.
    private const string ForeignSourceName = "NotOpenTelemetry-SelfDiagnosticsListenerTests-Foreign";

    private const string NoPayloadManifestSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-NoPayloadManifest";
    private const string EmptyPayloadSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-EmptyPayload";
    private const string CriticalSubscriptionSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-CriticalSubscription";
    private const string DisposedUpdateSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-DisposedUpdate";

    [Fact]
    public void SelfDescribingEvent_RendersPayloadNamesAndValues()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new EventSource(
            SelfDescribingSourceName,
            EventSourceSettings.EtwSelfDescribingEventFormat);

        eventSource.Write(
            "CustomEvent",
            new EventSourceOptions { Level = EventLevel.Warning },
            new SelfDescribingPayload { Detail = "payload value", Attempt = 3 });

        var message = WaitForEntryEndingWith(sink, "Attempt=3");

        Assert.StartsWith(SelfDescribingSourceName + ": ", message, StringComparison.Ordinal);
        Assert.Contains("Detail=payload value", message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventLevelBelowSubscription_IsNotDelivered()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Error), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Error);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new LevelTestEventSource();

        eventSource.WarningEvent("below the subscription level");
        eventSource.ErrorEvent("at the subscription level");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => sink.Written.Any(item => item.Entry.EventId.Id == 2)));
        Assert.DoesNotContain(sink.Written, item => item.Entry.EventId.Id == 1);
    }

    [Fact]
    public void ManifestMessageWithPayload_HasPayloadSubstituted()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new SubstitutedMessageEventSource();

        eventSource.WidgetFailed("gizmo", 4);

        var message = WaitForEntryEndingWith(sink, "Widget gizmo failed after 4 attempts");

        // Exact match: the placeholders are gone and the payload is not appended a second time.
        Assert.Equal(SubstitutedMessageSourceName + ": Widget gizmo failed after 4 attempts", message);
    }

    [Fact]
    public void ManifestMessageDisagreeingWithPayload_FallsBackToRawPayload()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new MismatchedMessageEventSource();

        try
        {
            eventSource.MismatchedMessage("only value");
        }
        catch (EventSourceException)
        {
            // Every subscribed listener is handed the event before the runtime surfaces this.
            // OpenTelemetrySdkEventSource.Listener - the SDK's own DEBUG-only diagnostic listener -
            // also string.Formats the manifest message and does not guard against the mismatch, and
            // .NET Framework rethrows a failing listener's exception at the WriteEvent call site.
            // That is a failure of that helper, not of the listener under test.
        }

        var message = WaitForEntryEndingWith(sink, "detail=only value");

        // Exact match: no fragment of the unusable format string may leak into the entry.
        Assert.Equal(MismatchedMessageSourceName + ": detail=only value", message);
    }

    [Fact]
    public void EventLevels_AreMappedToLogLevels()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Information), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Information);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new MappedLevelEventSource();

        eventSource.CriticalEvent("critical event");
        eventSource.LogAlwaysEvent("log always event");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => EntriesFrom(sink, MappedLevelSourceName).Count >= 2));

        var entries = EntriesFrom(sink, MappedLevelSourceName);

        Assert.Equal(LogLevel.Critical, entries.Single(entry => entry.EventId.Id == 1).Level);

        // LogAlways carries no severity of its own, so Information is the neutral landing spot.
        Assert.Equal(LogLevel.Information, entries.Single(entry => entry.EventId.Id == 2).Level);
    }

    [Fact]
    public void UpdateLevel_ReSubscribesAlreadySubscribedSources()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);

        // The dispatcher stays at Debug throughout so that every observation below is attributable
        // to the listener's EventSource subscription rather than to the dispatcher's own filter.
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Debug), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new ResubscribeEventSource();

        // Subscribed at Warning: the Verbose event must not be delivered. The Warning event behind
        // it is the ordering marker - the dispatcher queue is FIFO, so once the marker has been
        // written the Verbose event has already had its chance.
        eventSource.VerboseEvent("verbose before update");
        eventSource.WarningEvent("marker one");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(sink, "marker one")));
        Assert.False(WasWritten(sink, "verbose before update"));

        // Re-subscribing an already-subscribed source at Debug maps to EventLevel.Verbose.
        listener.UpdateLevel(LogLevel.Debug);
        eventSource.VerboseEvent("verbose after update");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(sink, "verbose after update")));

        // None disables the subscription outright. The marker is written straight to the logger so
        // that it still queues behind the suppressed event and keeps the assertion deterministic.
        listener.UpdateLevel(LogLevel.None);
        eventSource.VerboseEvent("verbose after disable");
        WriteMarker(logger, "marker two");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(sink, "marker two")));
        Assert.False(WasWritten(sink, "verbose after disable"));
    }

    [Fact]
    public void ForeignEventSourceEvent_IsIgnored()
    {
        // Regression: dotnet/runtime#31927 - EventCounter payloads are published to every
        // EventListener in the process regardless of which providers that listener enabled.
        // Without the name guard at the top of OnEventWritten the SDK's self-diagnostics file
        // fills up with counter events from unrelated components.
        //
        // The runtime's cross-delivery cannot be forced deterministically from a test, so the
        // delivery itself is simulated: a real EventWrittenEventArgs produced by a real
        // non-OpenTelemetry source is handed to the real (protected) callback from inside the
        // capturing callback, where the args are still valid.
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Debug), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Debug);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new ForeignEventSource();
        using var relay = new RelayEventListener(listener);

        relay.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
        eventSource.ForeignEvent("must not be logged");

        Assert.True(relay.Relayed, "the foreign event was never relayed into the listener");

        // A marker written after the relayed event proves the pipeline is live, so the absence of
        // the foreign event below is a rejection rather than a race.
        WriteMarker(logger, "foreign marker");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(sink, "foreign marker")));
        Assert.False(WasWritten(sink, "must not be logged"));
        Assert.DoesNotContain(
            sink.Written,
            item => item.Entry.Message.StartsWith(ForeignSourceName, StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestMessageWithNoPayload_MessageIsRenderedVerbatim()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new NoPayloadManifestEventSource();

        eventSource.NoPayloadEvent();

        var message = WaitForEntryEndingWith(sink, "Static manifest message");

        Assert.Equal(NoPayloadManifestSourceName + ": Static manifest message", message);
    }

    [Fact]
    public void SelfDescribingEventWithNoPayload_EntryArrives()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new EventSource(
            EmptyPayloadSourceName,
            EventSourceSettings.EtwSelfDescribingEventFormat);

        eventSource.Write("EmptyEvent", new EventSourceOptions { Level = EventLevel.Warning });

        WriteMarker(logger, "empty payload marker");
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(sink, "empty payload marker")));

        Assert.True(
            EntriesFrom(sink, EmptyPayloadSourceName).Count >= 1,
            "expected an entry from the self-describing source with empty payload");
    }

    [Fact]
    public void UpdateLevel_Critical_FiltersNonCriticalEvents()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Critical), 1, (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");

        using var eventSource = new CriticalSubscriptionEventSource();

        listener.UpdateLevel(LogLevel.Critical);
        eventSource.CriticalEvent("critical message");
        eventSource.ErrorEvent("error below critical");

        // Once the Critical entry has arrived the Error event has already been filtered by the
        // runtime (it is above EventLevel.Critical), so DoesNotContain is deterministic here.
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => EntriesFrom(sink, CriticalSubscriptionSourceName).Any(e => e.EventId.Id == 1)));
        Assert.DoesNotContain(
            EntriesFrom(sink, CriticalSubscriptionSourceName),
            e => e.EventId.Id == 2);
    }

    [Fact]
    public void UpdateLevel_AfterDispose_DoesNotThrow()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null);

        // Create the listener before the EventSource so that OnEventSourceCreated runs and the
        // source is recorded in subscribedSources - giving UpdateLevel something to iterate over.
        var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        using var eventSource = new EventSource(DisposedUpdateSourceName);

        listener.Dispose();

        var ex = Record.Exception(() => listener.UpdateLevel(LogLevel.Debug));
        Assert.Null(ex);
    }

    private static SelfDiagnosticsOptions.SelfDiagnosticsConfiguration CreateConfiguration(LogLevel minimumLevel)
        => SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(
            new SelfDiagnosticsOptions { LogToStdout = true, MinimumLevel = minimumLevel });

    /// <summary>
    /// Waits for the entry whose rendered message ends with <paramref name="messageTail"/> and
    /// returns it. Matching on the tail rather than on the source name alone keeps the assertion
    /// stable when the runtime interleaves an out-of-band <c>EventSourceMessage</c> from the same
    /// source (which it does whenever another listener in the process throws).
    /// </summary>
    private static string WaitForEntryEndingWith(TestSink sink, string messageTail)
    {
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(sink, messageTail)),
            $"no entry ending with '{messageTail}' was delivered");

        return sink.Written
            .First(item => item.Entry.Message.EndsWith(messageTail, StringComparison.Ordinal))
            .Entry.Message;
    }

    private static List<SelfDiagnosticsLogEntry> EntriesFrom(TestSink sink, string sourceName)
        => [.. sink.Written
            .Where(item => item.Entry.Message.StartsWith(sourceName, StringComparison.Ordinal))
            .Select(item => item.Entry)];

    /// <summary>
    /// Entries sourced from an <see cref="EventSource"/> are rendered as
    /// <c>"{sourceName}: {body}"</c>, so matching on the tail identifies one specific event body
    /// without hard-coding the prefix. Entries written straight to the logger are the body alone.
    /// </summary>
    private static bool WasWritten(TestSink sink, string messageTail)
        => sink.Written.Any(item => item.Entry.Message.EndsWith(messageTail, StringComparison.Ordinal));

    /// <summary>
    /// Writes an entry directly to the logger, bypassing the EventSource plumbing. Used as an
    /// ordering marker when the assertion is that an EventSource event was *not* delivered.
    /// </summary>
    private static void WriteMarker(SelfDiagnosticsLogger logger, string text)
    {
        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, text, null);
        logger.Write(in entry);
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

    [EventSource(Name = SubstitutedMessageSourceName)]
    private sealed class SubstitutedMessageEventSource : EventSource
    {
        [Event(1, Level = EventLevel.Warning, Message = "Widget {0} failed after {1} attempts")]
        public void WidgetFailed(string widget, int attempts) => this.WriteEvent(1, widget, attempts);
    }

    [EventSource(Name = MismatchedMessageSourceName)]
    private sealed class MismatchedMessageEventSource : EventSource
    {
        // Two placeholders, one payload value: string.Format cannot render this.
        [Event(1, Level = EventLevel.Warning, Message = "Mismatched {0} and {1}")]
        public void MismatchedMessage(string detail) => this.WriteEvent(1, detail);
    }

    [EventSource(Name = MappedLevelSourceName)]
    private sealed class MappedLevelEventSource : EventSource
    {
        [Event(1, Level = EventLevel.Critical, Message = "{0}")]
        public void CriticalEvent(string message) => this.WriteEvent(1, message);

        [Event(2, Level = EventLevel.LogAlways, Message = "{0}")]
        public void LogAlwaysEvent(string message) => this.WriteEvent(2, message);
    }

    [EventSource(Name = ResubscribeSourceName)]
    private sealed class ResubscribeEventSource : EventSource
    {
        [Event(1, Level = EventLevel.Verbose, Message = "{0}")]
        public void VerboseEvent(string message) => this.WriteEvent(1, message);

        [Event(2, Level = EventLevel.Warning, Message = "{0}")]
        public void WarningEvent(string message) => this.WriteEvent(2, message);
    }

    [EventSource(Name = ForeignSourceName)]
    private sealed class ForeignEventSource : EventSource
    {
        [Event(1, Level = EventLevel.Warning, Message = "{0}")]
        public void ForeignEvent(string message) => this.WriteEvent(1, message);
    }

    [EventSource(Name = NoPayloadManifestSourceName)]
    private sealed class NoPayloadManifestEventSource : EventSource
    {
        [Event(1, Level = EventLevel.Warning, Message = "Static manifest message")]
        public void NoPayloadEvent() => this.WriteEvent(1);
    }

    [EventSource(Name = CriticalSubscriptionSourceName)]
    private sealed class CriticalSubscriptionEventSource : EventSource
    {
        [Event(1, Level = EventLevel.Critical, Message = "{0}")]
        public void CriticalEvent(string message) => this.WriteEvent(1, message);

        [Event(2, Level = EventLevel.Error, Message = "{0}")]
        public void ErrorEvent(string message) => this.WriteEvent(2, message);
    }

    /// <summary>
    /// Stands in for the runtime's cross-source delivery: it subscribes to the foreign source and
    /// forwards the resulting <see cref="EventWrittenEventArgs"/> into the listener under test.
    /// </summary>
    private sealed class RelayEventListener : EventListener
    {
        private static readonly MethodInfo OnEventWrittenMethod =
            typeof(SelfDiagnosticsLoggingEventListener).GetMethod(
                "OnEventWritten",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "SelfDiagnosticsLoggingEventListener.OnEventWritten was not found.");

        private readonly SelfDiagnosticsLoggingEventListener target;
        private volatile bool relayed;

        internal RelayEventListener(SelfDiagnosticsLoggingEventListener target)
        {
            this.target = target;
        }

        internal bool Relayed => this.relayed;

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (!eventData.EventSource.Name.StartsWith(ForeignSourceName, StringComparison.Ordinal))
            {
                return;
            }

            this.relayed = true;
            OnEventWrittenMethod.Invoke(this.target, [eventData]);
        }
    }
}
