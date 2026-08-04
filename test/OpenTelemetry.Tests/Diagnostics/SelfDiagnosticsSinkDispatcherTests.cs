// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Diagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsSinkDispatcherTests
{
    [Fact]
    public void DeferredEntries_RetainCaptureTimeContext_AndDrainThroughLevelFilter()
    {
        // Regression: deferred entries must render with capture-time timestamp/thread, and
        // entries below the resolved minimum level must be discarded on drain.
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();

        var captureTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var debugEntry = new SelfDiagnosticsLogEntry(captureTime, 7, LogLevel.Debug, default, "debug entry", null, null);
        var warningEntry = new SelfDiagnosticsLogEntry(captureTime, 7, LogLevel.Warning, default, "warning entry", null, null);

        Assert.True(dispatcher.IsEnabled(LogLevel.Debug)); // deferred: buffer everything
        dispatcher.Enqueue(in debugEntry);
        dispatcher.Enqueue(in warningEntry);

        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));
        var written = Assert.Single(sink.Written);
        Assert.Equal("warning entry", written.Entry.Message);
        Assert.Equal(captureTime, written.Entry.TimestampUtc);
        Assert.Equal(7, written.Entry.ThreadId);
    }

    [Fact]
    public void QueueAtCapacity_DropsNewest_AndReportsDropCount()
    {
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(maxQueuedEntries: 3);
        using var sink = new TestSink();

        for (var i = 0; i < 5; i++)
        {
            var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, $"entry {i}", null);
            dispatcher.Enqueue(in entry);
        }

        Assert.Equal(2, dispatcher.DroppedCount);

        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));

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
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        var formatter = new CountingFormatter();
        using var sinkA = new TestSink(formatter);
        using var sinkB = new TestSink(formatter);

        Assert.True(dispatcher.Activate([sinkA, sinkB], LogLevel.Warning));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "shared", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sinkA.Written.Count == 1 && sinkB.Written.Count == 1));
        Assert.Equal(1, formatter.FormatCount);
        Assert.Equal("shared", sinkA.Written[0].Formatted);
        Assert.Equal("shared", sinkB.Written[0].Formatted);
    }

    [Fact]
    public void NullFormatterSink_ReceivesRawEntryOnly()
    {
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink(formatter: null);

        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "raw", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));
        Assert.Null(sink.Written[0].Formatted);
        Assert.Equal("raw", sink.Written[0].Entry.Message);
    }

    [Fact]
    public void UpdateSinks_DisposesRemovedSinksOnPump_AndKeepsRetained()
    {
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var removed = new TestSink();
        using var retained = new TestSink();

        Assert.True(dispatcher.Activate([removed, retained], LogLevel.Warning));
        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "before replacement", null);
        dispatcher.Enqueue(in entry);
        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => removed.Written.Count == 1 && retained.Written.Count == 1));

        Assert.True(dispatcher.UpdateSinks([retained]));

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => removed.Disposed));

        // The pump is a single logical consumer, not a pinned OS thread: each async wakeup may
        // resume on a different pool thread. The contract to verify is that disposal ran on the
        // pump, never synchronously on the UpdateSinks caller.
        Assert.NotEqual(Environment.CurrentManagedThreadId, removed.DisposeThreadId);
        Assert.False(retained.Disposed);
    }

    [Fact]
    public void ActivateAfterDispose_ReturnsFalse_AndInstallsNothing()
    {
        // Regression: the original design could create sinks after disposal (leaked file
        // handles and writer tasks). The dispatcher now rejects lifecycle mutations after
        // Dispose so the caller can dispose the sinks it created.
        var dispatcher = new SelfDiagnosticsSinkDispatcher();
        dispatcher.Dispose();

        using var sink = new TestSink();
        Assert.False(dispatcher.Activate([sink], LogLevel.Warning));
        Assert.False(dispatcher.UpdateSinks([sink]));
        Assert.False(dispatcher.IsEnabled(LogLevel.Critical));
    }

    [Fact]
    public void Dispose_DrainsPendingEntries_AndDisposesSinksOnPump()
    {
        var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();

        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));

        for (var i = 0; i < 10; i++)
        {
            var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, $"entry {i}", null);
            dispatcher.Enqueue(in entry);
        }

        dispatcher.Dispose();

        Assert.Equal(10, sink.Written.Count);
        Assert.True(sink.Disposed);

        // Writes and disposal both belong to the pump (any pool thread), never the caller.
        Assert.NotEqual(Environment.CurrentManagedThreadId, sink.WriteThreadId);
        Assert.NotEqual(Environment.CurrentManagedThreadId, sink.DisposeThreadId);
    }

    [Fact]
    public void IsEnabled_RespectsLevelAndSinkGates()
    {
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();

        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));
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
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();

        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));
        dispatcher.UpdateLevel(LogLevel.Debug);

        Assert.True(dispatcher.IsEnabled(LogLevel.Debug));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Debug, default, "debug entry", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.Written.Count == 1));
    }

    [Fact]
    public void LevelUpdate_DoesNotRetroactivelyFilterAcceptedEntries()
    {
        var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();

        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));
        var warning = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "before level update", null);
        dispatcher.Enqueue(in warning);

        dispatcher.UpdateLevel(LogLevel.Error);
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
        // Regression: a work item whose generation had already been applied returned early
        // without clearing the transition gates, leaving the pump buffering with no further
        // work item to release it. Callers only queue monotonic generations today, so this
        // drives the guarded path directly.
        using var stdout = new SynchronizedStringWriter();
        var manager = new SelfDiagnosticsSinkManager(
            static () => string.Empty,
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
    public void SinksFlushedAfterBurst()
    {
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();

        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "entry", null);
        dispatcher.Enqueue(in entry);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => sink.FlushCount > 0));
    }

    private static SelfDiagnosticsOptions.SelfDiagnosticsConfiguration CreateConfiguration(LogLevel minimumLevel)
        => SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(
            new SelfDiagnosticsOptions { LogToStdout = true, MinimumLevel = minimumLevel });
}
