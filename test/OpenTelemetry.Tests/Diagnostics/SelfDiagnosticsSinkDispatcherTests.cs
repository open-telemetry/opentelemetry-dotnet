// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsSinkDispatcherTests
{
    [Fact]
    public void DeferredEntries_RetainCaptureTimeContext_AndDrainThroughLevelFilter()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        var captureTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var debugEntry = new SelfDiagnosticsLogEntry(captureTime, 7, LogLevel.Debug, default, "debug entry", null, null);
        var warningEntry = new SelfDiagnosticsLogEntry(captureTime, 7, LogLevel.Warning, default, "warning entry", null, null);

        Assert.True(dispatcher.IsEnabled(LogLevel.Debug)); // deferred: buffer everything
        dispatcher.Enqueue(in debugEntry);
        dispatcher.Enqueue(in warningEntry);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));
        var written = Assert.Single(sink.Written);
        Assert.Equal("warning entry", written.Entry.Message);
        Assert.Equal(captureTime, written.Entry.TimestampUtc);
        Assert.Equal(7, written.Entry.ThreadId);
    }

    [Fact]
    public void QueueAtCapacity_DropsNewest_AndReportsDropCount()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(maxQueuedEntries: 3, sinkResolver: _ => [sink]);

        for (var i = 0; i < 5; i++)
        {
            var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, $"entry {i}", null);
            dispatcher.Enqueue(in entry);
        }

        Assert.Equal(2, dispatcher.DroppedCount);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        // The three oldest entries survive (drop-newest) plus one drop-summary warning.
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 4));
        Assert.Equal("entry 0", sink.Written[0].Entry.Message);
        Assert.Equal("entry 1", sink.Written[1].Entry.Message);
        Assert.Equal("entry 2", sink.Written[2].Entry.Message);
        Assert.Contains("2 self-diagnostics entries were dropped", sink.Written[3].Entry.Message, StringComparison.Ordinal);
        Assert.Equal(0, dispatcher.DroppedCount);
    }

    [Fact]
    public void SinksSharingFormatterInstance_FormatOncePerEntry()
    {
        var formatter = new CountingFormatter();
        using var sinkA = new TestSink(formatter);
        using var sinkB = new TestSink(formatter);
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sinkA, sinkB]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "shared", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sinkA.Written.Count == 1 && sinkB.Written.Count == 1));
        Assert.Equal(1, formatter.FormatCount);
        Assert.Equal("shared", sinkA.Written[0].Formatted);
        Assert.Equal("shared", sinkB.Written[0].Formatted);
    }

    [Fact]
    public void NonContiguousSinksSharingFormatterInstance_FormatOncePerEntry()
    {
        var sharedFormatter = new CountingFormatter();
        var otherFormatter = new CountingFormatter();
        using var firstSharedSink = new TestSink(sharedFormatter);
        using var otherSink = new TestSink(otherFormatter);
        using var secondSharedSink = new TestSink(sharedFormatter);
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(
            sinkResolver: _ => [firstSharedSink, otherSink, secondSharedSink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "shared", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => firstSharedSink.Written.Count == 1
                && otherSink.Written.Count == 1
                && secondSharedSink.Written.Count == 1));
        Assert.Equal(1, sharedFormatter.FormatCount);
        Assert.Equal(1, otherFormatter.FormatCount);
    }

    [Fact]
    public void NullFormatterSink_ReceivesRawEntryOnly()
    {
        using var sink = new TestSink(formatter: null);
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "raw", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));
        Assert.Null(sink.Written[0].Formatted);
        Assert.Equal("raw", sink.Written[0].Entry.Message);
    }

    [Fact]
    public void UpdateSinks_DisposesRemovedSinksOnPump_AndKeepsRetained()
    {
        using var removed = new TestSink();
        using var retained = new TestSink();

        ISelfDiagnosticsSink[] currentSinks = [removed, retained];
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => currentSinks);
        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "before replacement", null);
        dispatcher.Enqueue(in entry);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => removed.Written.Count == 1 && retained.Written.Count == 1));

        currentSinks = [retained];
        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 2, null));

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => removed.Disposed));

        // Disposal belongs to the dedicated pump thread, never the QueueConfiguration caller.
        Assert.NotEqual(Environment.CurrentManagedThreadId, removed.DisposeThreadId);
        Assert.False(retained.Disposed);
    }

    [Fact]
    public void QueueConfiguration_AfterDispose_ReturnsFalse()
    {
        using var sink = new TestSink();
        var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        dispatcher.Dispose();

        Assert.False(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));
        Assert.False(dispatcher.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void Dispose_DrainsPendingEntries_AndDisposesSinksOnPump()
    {
        using var sink = new TestSink();
        var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        for (var i = 0; i < 10; i++)
        {
            var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, $"entry {i}", null);
            dispatcher.Enqueue(in entry);
        }

        dispatcher.Dispose();

        Assert.Equal(10, sink.Written.Count);
        Assert.True(sink.Disposed);

        // Writes and disposal both belong to the dedicated pump thread, never the caller.
        Assert.NotEqual(Environment.CurrentManagedThreadId, sink.WriteThreadId);
        Assert.NotEqual(Environment.CurrentManagedThreadId, sink.DisposeThreadId);
    }

    [Fact]
    public void IsEnabled_RespectsLevelAndSinkGates()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));
        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "activation barrier", null);
        dispatcher.Enqueue(in entry);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));

        Assert.False(dispatcher.IsEnabled(LogLevel.Debug));
        Assert.False(dispatcher.IsEnabled(LogLevel.None));
        Assert.True(dispatcher.IsEnabled(LogLevel.Warning));

        sink.Enabled = false;
        Assert.False(dispatcher.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public void UpdateLevel_TakesEffectForSubsequentEntries()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));
        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Debug), 2, null));

        Assert.True(dispatcher.IsEnabled(LogLevel.Debug));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Debug, default, "debug entry", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));
    }

    [Fact]
    public void LevelUpdate_DoesNotRetroactivelyFilterAcceptedEntries()
    {
        using var sink = new TestSink();
        var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        using var firstApplied = new ManualResetEventSlim(false);
        Assert.True(dispatcher.QueueConfiguration(
            CreateConfiguration(LogLevel.Warning),
            1,
            (_, _, _) => firstApplied.Set()));
        Assert.True(
            firstApplied.Wait(TimeSpan.FromSeconds(5)),
            "Configuration was not applied by the pump within the timeout");

        var warning = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "before level update", null);
        dispatcher.Enqueue(in warning);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Error), 2, null));
        var error = SelfDiagnosticsLogEntry.Capture(LogLevel.Error, default, "after level update", null);
        dispatcher.Enqueue(in error);
        dispatcher.Dispose();

        Assert.Equal(2, sink.Written.Count);
        Assert.Equal("before level update", sink.Written[0].Entry.Message);
        Assert.Equal("after level update", sink.Written[1].Entry.Message);
    }

    [Fact]
    public void SupersededConfigurationGeneration_ClearsTheDeferredGate()
    {
        using var stdout = new SynchronizedStringWriter();
        var manager = new SelfDiagnosticsSinkManager(
            static _ => string.Empty,
            static _ => { },
            stdoutWriter: () => stdout,
            stderrWriter: () => stdout);

        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        dispatcher.SetSinkManager(manager);

        var configuration = CreateConfiguration(LogLevel.Warning);
        Assert.True(dispatcher.QueueConfiguration(configuration, 5, null));

        var applied = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "after apply", null);
        dispatcher.Enqueue(in applied);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("after apply", StringComparison.Ordinal)));

        // Re-enter the buffering state, then hand the pump a generation it has already applied.
        dispatcher.PreparePending(configuration);
        Assert.True(dispatcher.QueueConfiguration(configuration, 3, null));

        var afterStale = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "after stale generation", null);
        dispatcher.Enqueue(in afterStale);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("after stale generation", StringComparison.Ordinal)));
    }

    [Fact]
    public void PendingNewerConfiguration_RetainsItsProducerGateWhileOlderConfigurationFinishes()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"otel-dispatcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        using var firstConfigurationApplying = new ManualResetEventSlim();
        using var continueFirstConfiguration = new ManualResetEventSlim();
        using var firstConfigurationApplied = new ManualResetEventSlim();
        using var continueFirstCallback = new ManualResetEventSlim();
        using var stdout = new SynchronizedStringWriter();

        string BlockFirstPreamble(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
        {
            firstConfigurationApplying.Set();
            continueFirstConfiguration.Wait();
            return string.Empty;
        }

        var manager = new SelfDiagnosticsSinkManager(
            BlockFirstPreamble,
            static _ => { },
            stdoutWriter: () => stdout,
            stderrWriter: () => stdout);
        var dispatcher = new SelfDiagnosticsSinkDispatcher();
        dispatcher.SetSinkManager(manager);

        try
        {
            var firstConfiguration = SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(
                new SelfDiagnosticsOptions { LogDirectory = directory, MinimumLevel = LogLevel.Debug });
            var secondConfiguration = CreateConfiguration(LogLevel.Error);

            Assert.True(dispatcher.QueueConfiguration(
                firstConfiguration,
                generation: 1,
                appliedCallback: (_, _, _) =>
                {
                    firstConfigurationApplied.Set();
                    continueFirstCallback.Wait();
                }));
            Assert.True(firstConfigurationApplying.Wait(TimeSpan.FromSeconds(5)));

            Assert.True(dispatcher.QueueConfiguration(secondConfiguration, generation: 2, appliedCallback: null));
            continueFirstConfiguration.Set();
            Assert.True(firstConfigurationApplied.Wait(TimeSpan.FromSeconds(5)));

            Assert.False(dispatcher.IsEnabled(LogLevel.Debug));
        }
        finally
        {
            continueFirstConfiguration.Set();
            continueFirstCallback.Set();
            dispatcher.Dispose();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SinksFlushedAfterBurst()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "entry", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.FlushCount > 0));
    }

    [Fact]
    public void SinkWriteThrows_OtherSinksStillReceiveTheEntry_AndThePumpSurvives()
    {
        using var broken = new ThrowingSink(throwOnWrite: true);
        using var healthy = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [broken, healthy]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var first = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "first", null);
        dispatcher.Enqueue(in first);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => healthy.Written.Count == 1));

        var second = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "second", null);
        dispatcher.Enqueue(in second);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => healthy.Written.Count == 2));

        // The broken sink is first in the set, so both entries reached it before the healthy
        // sink recorded them: the failure was swallowed per sink, not per entry.
        Assert.Equal(2, broken.WriteAttempts);
        Assert.Equal("first", healthy.Written[0].Entry.Message);
        Assert.Equal("second", healthy.Written[1].Entry.Message);

        var pump = dispatcher.PumpThread;
        Assert.NotNull(pump);
        Assert.True(pump!.IsAlive);
    }

    [Fact]
    public void FormatterThrows_OtherSinksStillReceiveTheEntry_AndThePumpSurvives()
    {
        var brokenFormatter = new ThrowingFormatter();
        var workingFormatter = new CountingFormatter();
        using var brokenFormatSink = new TestSink(brokenFormatter);
        using var healthy = new TestSink(workingFormatter);
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [brokenFormatSink, healthy]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var first = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "first", null);
        dispatcher.Enqueue(in first);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => healthy.Written.Count == 1));

        var second = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "second", null);
        dispatcher.Enqueue(in second);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => healthy.Written.Count == 2));

        Assert.Equal(2, brokenFormatter.FormatAttempts);
        Assert.Empty(brokenFormatSink.Written);
        Assert.Equal(2, workingFormatter.FormatCount);

        var pump = dispatcher.PumpThread;
        Assert.NotNull(pump);
        Assert.True(pump!.IsAlive);
    }

    [Fact]
    public void SinksSharingThrowingFormatter_AttemptFormattingOnlyOncePerEntry()
    {
        var formatter = new ThrowingFormatter();
        using var firstSink = new TestSink(formatter);
        using var secondSink = new TestSink(formatter);
        using var healthySink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(
            sinkResolver: _ => [firstSink, healthySink, secondSink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "shared failure", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => healthySink.Written.Count == 1));
        Assert.Equal(1, formatter.FormatAttempts);
        Assert.Empty(firstSink.Written);
        Assert.Empty(secondSink.Written);
    }

    [Fact]
    public void SinkFlushThrows_DoesNotKillThePump_AndOtherSinksStillFlush()
    {
        using var broken = new ThrowingSink(throwOnFlush: true);
        using var healthy = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [broken, healthy]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var first = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "first", null);
        dispatcher.Enqueue(in first);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => healthy.FlushCount > 0));
        Assert.True(broken.FlushAttempts > 0);

        var second = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "second", null);
        dispatcher.Enqueue(in second);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => healthy.Written.Count == 2));

        var pump = dispatcher.PumpThread;
        Assert.NotNull(pump);
        Assert.True(pump!.IsAlive);
    }

    [Fact]
    public void SinkDisposeThrows_DuringConfigurationUpdate_DoesNotKillThePump()
    {
        using var broken = new ThrowingSink(throwOnFirstDispose: true);
        using var replacement = new TestSink();

        ISelfDiagnosticsSink[] currentSinks = [broken];
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => currentSinks);

        using var firstApplied = new ManualResetEventSlim(false);
        Assert.True(dispatcher.QueueConfiguration(
            CreateConfiguration(LogLevel.Warning),
            1,
            (_, _, _) => firstApplied.Set()));
        Assert.True(
            firstApplied.Wait(TimeSpan.FromSeconds(5)),
            "Configuration was not applied by the pump within the timeout");

        currentSinks = [replacement];
        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 2, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "after replacement", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => replacement.Written.Count == 1));
        Assert.Equal(1, broken.DisposeAttempts);

        // Both configuration and entry were queued in order, so the broken sink was already out
        // of the set before the entry was written: it must never have been written to.
        Assert.Equal(0, broken.WriteAttempts);

        var pump = dispatcher.PumpThread;
        Assert.NotNull(pump);
        Assert.True(pump!.IsAlive);
    }

    [Fact]
    public void SinkDisposeThrows_DuringDispatcherDispose_DoesNotAbortTheDrain()
    {
        using var broken = new ThrowingSink(throwOnFirstDispose: true);
        using var healthy = new TestSink();

        var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [broken, healthy]);
        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "drained", null);
        dispatcher.Enqueue(in entry);

        dispatcher.Dispose();

        Assert.Equal(1, broken.WriteAttempts);
        Assert.Single(healthy.Written);
        Assert.Equal(1, broken.DisposeAttempts);
        Assert.True(healthy.Disposed);
    }

    [Fact]
    public void SetSinkManager_AfterActivation_Throws()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var manager = new SelfDiagnosticsSinkManager(static _ => string.Empty, static _ => { });
        Assert.Throws<InvalidOperationException>(() => dispatcher.SetSinkManager(manager));
    }

    [Fact]
    public void DeferredMode_BuffersEntries_UntilConfigurationArrives()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        // Before QueueConfiguration: no pump thread, deferred mode admits everything.
        Assert.Null(dispatcher.PumpThread);
        Assert.True(dispatcher.IsEnabled(LogLevel.Trace));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "buffered", null);
        dispatcher.Enqueue(in entry);
        Assert.Empty(sink.Written);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));
        Assert.Equal("buffered", sink.Written[0].Entry.Message);
    }

    [Fact]
    public void Dispose_WhenThePumpWasNeverStarted_DrainsTheBufferWithoutThrowing()
    {
        var dispatcher = new SelfDiagnosticsSinkDispatcher(maxQueuedEntries: 4);

        for (var i = 0; i < 3; i++)
        {
            var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, $"entry {i}", null);
            dispatcher.Enqueue(in entry);
        }

        Assert.Null(dispatcher.PumpThread);

        dispatcher.Dispose();

        Assert.Null(dispatcher.PumpThread);
        Assert.Equal(0, dispatcher.DroppedCount);
        Assert.False(dispatcher.IsEnabled(LogLevel.Critical));

        // Dispose is idempotent: the second call must not touch the already-disposed semaphore.
        dispatcher.Dispose();
    }

    [Fact]
    public void ReportInternalError_EnqueuesAnErrorEntryForTheSinks()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        dispatcher.ReportInternalError("self-diagnostics machinery failed");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));
        Assert.Equal(LogLevel.Error, sink.Written[0].Entry.Level);
        Assert.Equal("self-diagnostics machinery failed", sink.Written[0].Entry.Message);
    }

    [Fact]
    public void ReportInternalError_WhenTheLevelIsNotAdmitted_QueuesNothing()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var barrier = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "barrier", null);
        dispatcher.Enqueue(in barrier);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Critical), 2, null));
        Assert.False(dispatcher.IsEnabled(LogLevel.Error));
        dispatcher.ReportInternalError("suppressed machinery failure");

        var after = SelfDiagnosticsLogEntry.Capture(LogLevel.Critical, default, "after", null);
        dispatcher.Enqueue(in after);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 2));

        // The queue is FIFO: had the error been accepted it would occupy this slot.
        Assert.Equal("after", sink.Written[1].Entry.Message);
    }

    [Fact]
    public void DisabledSink_DoesNotCauseAFormat()
    {
        var disabledFormatter = new CountingFormatter();
        var enabledFormatter = new CountingFormatter();
        using var disabled = new TestSink(disabledFormatter) { Enabled = false };
        using var enabled = new TestSink(enabledFormatter);
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [disabled, enabled]);

        Assert.True(dispatcher.QueueConfiguration(CreateConfiguration(LogLevel.Warning), 1, null));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "single format", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => enabled.Written.Count == 1));
        Assert.Equal(0, disabledFormatter.FormatCount);
        Assert.Equal(1, enabledFormatter.FormatCount);
        Assert.Empty(disabled.Written);
    }

    private static SelfDiagnosticsOptions.SelfDiagnosticsConfiguration CreateConfiguration(LogLevel minimumLevel)
        => SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(
            new SelfDiagnosticsOptions { LogToStdout = true, MinimumLevel = minimumLevel });

    private sealed class ThrowingSink : ISelfDiagnosticsSink
    {
        private readonly bool throwOnWrite;
        private readonly bool throwOnFlush;
        private readonly bool throwOnFirstDispose;
        private int writeAttempts;
        private int flushAttempts;
        private int disposeAttempts;

        public ThrowingSink(
            bool throwOnWrite = false,
            bool throwOnFlush = false,
            bool throwOnFirstDispose = false,
            ISelfDiagnosticsFormatter? formatter = null)
        {
            this.throwOnWrite = throwOnWrite;
            this.throwOnFlush = throwOnFlush;
            this.throwOnFirstDispose = throwOnFirstDispose;
            this.Formatter = formatter;
        }

        public ISelfDiagnosticsFormatter? Formatter { get; }

        public int WriteAttempts => Volatile.Read(ref this.writeAttempts);

        public int FlushAttempts => Volatile.Read(ref this.flushAttempts);

        public int DisposeAttempts => Volatile.Read(ref this.disposeAttempts);

        public bool IsEnabled(LogLevel level) => true;

        public void Write(in SelfDiagnosticsLogEntry entry, string? formatted)
        {
            Interlocked.Increment(ref this.writeAttempts);
            if (this.throwOnWrite)
            {
                throw new InvalidOperationException("sink write failed");
            }
        }

        public void OnInstalled()
        {
        }

        public void Flush()
        {
            Interlocked.Increment(ref this.flushAttempts);
            if (this.throwOnFlush)
            {
                throw new InvalidOperationException("sink flush failed");
            }
        }

        public void Dispose()
        {
            // Only the first disposal throws, so the test's own `using` can dispose the sink a
            // second time without failing the test.
            if (Interlocked.Increment(ref this.disposeAttempts) == 1 && this.throwOnFirstDispose)
            {
                throw new InvalidOperationException("sink dispose failed");
            }
        }
    }

    private sealed class ThrowingFormatter : ISelfDiagnosticsFormatter
    {
        private int formatAttempts;

        public int FormatAttempts => Volatile.Read(ref this.formatAttempts);

        public string? FileHeader => null;

        public string Format(in SelfDiagnosticsLogEntry entry)
        {
            Interlocked.Increment(ref this.formatAttempts);
            throw new InvalidOperationException("formatter failed");
        }
    }
}
