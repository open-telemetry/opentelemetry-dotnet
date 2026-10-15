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
    private const string ForeignSourceName = "NotOpenTelemetry-SelfDiagnosticsListenerTests-Foreign";
    private const string NoPayloadManifestSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-NoPayloadManifest";
    private const string EmptyPayloadSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-EmptyPayload";
    private const string CriticalSubscriptionSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-CriticalSubscription";
    private const string DisposedUpdateSourceName = "OpenTelemetry-SelfDiagnosticsListenerTests-DisposedUpdate";

    [Fact]
    public void SelfDescribingEvent_RendersPayloadNamesAndValues()
    {
        using var context = ListenerTestContext.Create(LogLevel.Warning, LogLevel.Warning);

        using var eventSource = new EventSource(
            SelfDescribingSourceName,
            EventSourceSettings.EtwSelfDescribingEventFormat);

        eventSource.Write(
            "CustomEvent",
            new EventSourceOptions { Level = EventLevel.Warning },
            new SelfDescribingPayload { Detail = "payload value", Attempt = 3 });

        var message = WaitForEntryEndingWith(context.Sink, "Attempt=3");

        Assert.StartsWith(SelfDescribingSourceName + ": ", message, StringComparison.Ordinal);
        Assert.Contains("Detail=payload value", message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventLevelBelowSubscription_IsNotDelivered()
    {
        using var context = ListenerTestContext.Create(LogLevel.Error, LogLevel.Error);

        using var eventSource = new LevelTestEventSource();

        eventSource.WarningEvent("below the subscription level");
        eventSource.ErrorEvent("at the subscription level");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => context.Sink.Written.Any(item => item.Entry.EventId.Id == 2)),
            "expected the Error event (id 2) to be delivered");
        Assert.DoesNotContain(context.Sink.Written, item => item.Entry.EventId.Id == 1);
    }

    [Fact]
    public void ManifestMessageWithPayload_HasPayloadSubstituted()
    {
        using var context = ListenerTestContext.Create(LogLevel.Warning, LogLevel.Warning);

        using var eventSource = new SubstitutedMessageEventSource();

        eventSource.WidgetFailed("gizmo", 4);

        var message = WaitForEntryEndingWith(context.Sink, "Widget gizmo failed after 4 attempts");

        Assert.Equal(SubstitutedMessageSourceName + ": Widget gizmo failed after 4 attempts", message);
    }

    [Fact]
    public void ManifestMessageDisagreeingWithPayload_FallsBackToRawPayload()
    {
        using var context = ListenerTestContext.Create(LogLevel.Warning, LogLevel.Warning);

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

        var message = WaitForEntryEndingWith(context.Sink, "detail=only value");

        Assert.Equal(MismatchedMessageSourceName + ": detail=only value", message);
    }

    [Fact]
    public void EventLevels_AreMappedToLogLevels()
    {
        using var context = ListenerTestContext.Create(LogLevel.Information, LogLevel.Information);

        using var eventSource = new MappedLevelEventSource();

        eventSource.CriticalEvent("critical event");
        eventSource.LogAlwaysEvent("log always event");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
                () => EntriesFrom(context.Sink, MappedLevelSourceName).Count >= 2),
            $"expected at least 2 entries from {MappedLevelSourceName}, found {EntriesFrom(context.Sink, MappedLevelSourceName).Count}");

        var entries = EntriesFrom(context.Sink, MappedLevelSourceName);

        Assert.Equal(LogLevel.Critical, entries.Single(entry => entry.EventId.Id == 1).Level);

        // LogAlways carries no severity of its own, so Information is the neutral landing spot.
        Assert.Equal(LogLevel.Information, entries.Single(entry => entry.EventId.Id == 2).Level);
    }

    [Fact]
    public void UpdateLevel_ReSubscribesAlreadySubscribedSources()
    {
        // The dispatcher stays at Debug throughout so observations reflect the listener subscription.
        using var context = ListenerTestContext.Create(LogLevel.Debug, LogLevel.Warning);

        using var eventSource = new ResubscribeEventSource();

        // Subscribed at Warning: the Verbose event must not be delivered. The Warning marker event
        // follows it; the dispatcher queue is FIFO, so once the marker is written the Verbose event
        // has already had its chance.
        eventSource.VerboseEvent("verbose before update");
        eventSource.WarningEvent("marker one");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(context.Sink, "marker one")),
            "expected marker one to arrive before asserting verbose was suppressed");
        Assert.False(
            WasWritten(context.Sink, "verbose before update"),
            $"expected Verbose event to be suppressed at Warning subscription; sink entries: {DescribeSinkMessages(context.Sink)}");

        // Re-subscribing an already-subscribed source at Debug maps to EventLevel.Verbose.
        context.Listener.UpdateLevel(LogLevel.Debug);
        eventSource.VerboseEvent("verbose after update");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(context.Sink, "verbose after update")),
            "expected verbose after update once the subscription level was lowered");

        // None disables the subscription. The marker is written directly so it still queues behind
        // the suppressed event and keeps the negative assertion deterministic.
        context.Listener.UpdateLevel(LogLevel.None);
        eventSource.VerboseEvent("verbose after disable");
        WriteMarker(context.Logger, "marker two");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(context.Sink, "marker two")),
            "expected marker two to arrive before asserting verbose was suppressed after disable");
        Assert.False(
            WasWritten(context.Sink, "verbose after disable"),
            $"expected Verbose event to be suppressed after disabling subscription; sink entries: {DescribeSinkMessages(context.Sink)}");
    }

    [Fact]
    public void ForeignEventSourceEvent_IsIgnored()
    {
        // dotnet/runtime#31927 - EventCounter payloads are published to every
        // EventListener in the process regardless of which providers that listener enabled.
        // Without the name guard at the top of OnEventWritten the SDK's self-diagnostics file
        // fills up with counter events from unrelated components.
        //
        // The runtime's cross-delivery cannot be forced deterministically from a test, so the
        // delivery itself is simulated: a real EventWrittenEventArgs produced by a real
        // non-OpenTelemetry source is handed to the real (protected) callback from inside the
        // capturing callback, where the args are still valid.
        using var context = ListenerTestContext.Create(LogLevel.Debug, LogLevel.Debug);

        using var eventSource = new ForeignEventSource();
        using var relay = new RelayEventListener(context.Listener);

        relay.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
        eventSource.ForeignEvent("must not be logged");

        Assert.True(relay.Relayed, "the foreign event was never relayed into the listener");

        // Marker after the relayed event: pipeline is live, so absence below is rejection not race.
        WriteMarker(context.Logger, "foreign marker");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(context.Sink, "foreign marker")),
            "expected foreign marker to arrive before asserting the foreign event was ignored");
        Assert.False(
            WasWritten(context.Sink, "must not be logged"),
            $"expected foreign EventSource event to be ignored; sink entries: {DescribeSinkMessages(context.Sink)}");
        Assert.DoesNotContain(
            context.Sink.Written,
            item => item.Entry.Message.StartsWith(ForeignSourceName, StringComparison.Ordinal));
    }

    [Fact]
    public void ManifestMessageWithNoPayload_MessageIsRenderedVerbatim()
    {
        using var context = ListenerTestContext.Create(LogLevel.Warning, LogLevel.Warning);

        using var eventSource = new NoPayloadManifestEventSource();

        eventSource.NoPayloadEvent();

        var message = WaitForEntryEndingWith(context.Sink, "Static manifest message");

        Assert.Equal(NoPayloadManifestSourceName + ": Static manifest message", message);
    }

    [Fact]
    public void SelfDescribingEventWithNoPayload_EntryArrives()
    {
        using var context = ListenerTestContext.Create(LogLevel.Warning, LogLevel.Warning);

        using var eventSource = new EventSource(
            EmptyPayloadSourceName,
            EventSourceSettings.EtwSelfDescribingEventFormat);

        eventSource.Write("EmptyEvent", new EventSourceOptions { Level = EventLevel.Warning });

        WriteMarker(context.Logger, "empty payload marker");
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => WasWritten(context.Sink, "empty payload marker")),
            "expected empty payload marker to arrive before checking for the EventSource entry");

        Assert.True(
            EntriesFrom(context.Sink, EmptyPayloadSourceName).Count >= 1,
            "expected an entry from the self-describing source with empty payload");
    }

    [Fact]
    public void UpdateLevel_Critical_FiltersNonCriticalEvents()
    {
        using var context = ListenerTestContext.Create(LogLevel.Critical, LogLevel.Warning);

        using var eventSource = new CriticalSubscriptionEventSource();

        context.Listener.UpdateLevel(LogLevel.Critical);
        eventSource.CriticalEvent("critical message");
        eventSource.ErrorEvent("error below critical");

        // Once the Critical entry has arrived the Error event has already been filtered by the
        // runtime (it is above EventLevel.Critical), so DoesNotContain is deterministic here.
        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
                () => EntriesFrom(context.Sink, CriticalSubscriptionSourceName).Any(e => e.EventId.Id == 1)),
            $"expected a Critical event (id 1) from {CriticalSubscriptionSourceName}");
        Assert.DoesNotContain(
            EntriesFrom(context.Sink, CriticalSubscriptionSourceName),
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

    private static bool WasWritten(TestSink sink, string messageTail)
        => sink.Written.Any(item => item.Entry.Message.EndsWith(messageTail, StringComparison.Ordinal));

    private static string DescribeSinkMessages(TestSink sink)
        => sink.Written.Count == 0
            ? "(empty)"
            : string.Join("; ", sink.Written.Select(item => item.Entry.Message));

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

    private sealed class ListenerTestContext : IDisposable
    {
        private readonly SelfDiagnosticsSinkDispatcher dispatcher;
        private readonly SelfDiagnosticsLoggingEventListener listener;

        private ListenerTestContext(
            TestSink sink,
            SelfDiagnosticsSinkDispatcher dispatcher,
            SelfDiagnosticsLogger logger,
            SelfDiagnosticsLoggingEventListener listener)
        {
            this.Sink = sink;
            this.dispatcher = dispatcher;
            this.Logger = logger;
            this.listener = listener;
        }

        internal TestSink Sink { get; }

        internal SelfDiagnosticsLogger Logger { get; }

        internal SelfDiagnosticsLoggingEventListener Listener => this.listener;

        public void Dispose()
        {
            this.listener.Dispose();
            this.Logger.Dispose();
            this.dispatcher.Dispose();
            this.Sink.Dispose();
        }

        internal static ListenerTestContext Create(
            LogLevel dispatcherLevel,
            LogLevel listenerLevel,
            int configurationGeneration = 1)
        {
            var sink = new TestSink();
            var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
            var logger = new SelfDiagnosticsLogger(
                new SelfDiagnosticsOptions(),
                static _ => string.Empty,
                dispatcher: dispatcher,
                startImmediately: false);
            using var applied = new ManualResetEventSlim(false);
            dispatcher.QueueConfiguration(
                CreateConfiguration(dispatcherLevel),
                configurationGeneration,
                (_, _, _) => applied.Set());
            var listener = new SelfDiagnosticsLoggingEventListener(logger, listenerLevel);
            Assert.True(
                applied.Wait(TimeSpan.FromSeconds(5)),
                "Configuration was not applied by the pump within the timeout");

            return new ListenerTestContext(sink, dispatcher, logger, listener);
        }
    }
}
