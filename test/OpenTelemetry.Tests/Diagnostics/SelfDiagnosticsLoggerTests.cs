// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsLoggerTests
{
    [Fact]
    public void NonDeferred_LogFlowsToConsoleSink()
    {
        using var stdout = new SynchronizedStringWriter();
        using var logger = CreateConsoleLogger(new SelfDiagnosticsOptions { LogToStdout = true }, stdout);

        logger.Log(LogLevel.Warning, default, "hello diagnostics", null, static (m, _) => m);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("hello diagnostics", StringComparison.Ordinal)),
            "expected the log entry to reach the console sink");
        Assert.Contains("[Warning]", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NonDeferred_EntriesBelowMinimumLevel_AreFiltered()
    {
        using var stdout = new SynchronizedStringWriter();
        using var logger = CreateConsoleLogger(
            new SelfDiagnosticsOptions { LogToStdout = true, MinimumLevel = LogLevel.Warning },
            stdout);

        Assert.False(logger.IsEnabled(LogLevel.Debug));

        logger.Log(LogLevel.Debug, default, "should not appear", null, static (m, _) => m);
        logger.Log(LogLevel.Warning, default, "should appear", null, static (m, _) => m);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("should appear", StringComparison.Ordinal)),
            "expected the Warning entry to reach the console sink");
        Assert.DoesNotContain("should not appear", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PendingActivation_BuffersUntilApplyConfiguration_ThenDiscardsBelowResolvedLevel()
    {
        using var stdout = new SynchronizedStringWriter();
        var options = new SelfDiagnosticsOptions
        {
            LogToStdout = true,
            MinimumLevel = LogLevel.Debug,
        };

        using var logger = CreateConsoleLogger(options, stdout, startImmediately: false);

        Assert.True(logger.IsEnabled(LogLevel.Debug));
        logger.Log(LogLevel.Debug, default, "buffered debug", null, static (m, _) => m);
        logger.Log(LogLevel.Warning, default, "buffered warning", null, static (m, _) => m);
        Assert.Equal(string.Empty, stdout.ToString());

        logger.ApplyOptions(new SelfDiagnosticsOptions
        {
            LogToStdout = true,
            MinimumLevel = LogLevel.Warning,
        });

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("buffered warning", StringComparison.Ordinal)),
            "expected the buffered Warning entry to drain after ApplyOptions");
        Assert.DoesNotContain("buffered debug", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationAppliedCallback_ReportsSinkPresence()
    {
        var options = new SelfDiagnosticsOptions(); // no sinks configured
        using var logger = new SelfDiagnosticsLogger(options, static _ => string.Empty, startImmediately: false);

        bool? callbackHasSinks = null;
        logger.ConfigurationApplied = (_, hasSinks, _) => callbackHasSinks = hasSinks;

        logger.ApplyOptions(options);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => callbackHasSinks.HasValue),
            "expected ConfigurationApplied to run after ApplyOptions");
        Assert.False(
            callbackHasSinks,
            "expected ConfigurationApplied to report no configured sinks");
        Assert.False(
            logger.IsEnabled(LogLevel.Critical),
            "expected logger to stay disabled when no sinks are configured");
    }

    [Fact]
    public void ApplyOptionsAfterDispose_CreatesNothingAndDoesNotThrow()
    {
        using var stdout = new SynchronizedStringWriter();
        var options = new SelfDiagnosticsOptions { LogToStdout = true };
        var logger = CreateConsoleLogger(options, stdout);

        logger.Dispose();

        logger.ApplyOptions(new SelfDiagnosticsOptions { LogToStdout = true, MinimumLevel = LogLevel.Trace });
        logger.Log(LogLevel.Critical, default, "after dispose", null, static (m, _) => m);

        Assert.False(logger.IsEnabled(LogLevel.Critical));
        Assert.DoesNotContain("after dispose", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void HotReload_ConsoleSinkTogglesOffAndOn()
    {
        using var stdout = new SynchronizedStringWriter();
        var enabled = new SelfDiagnosticsOptions { LogToStdout = true };
        using var logger = CreateConsoleLogger(enabled, stdout);

        logger.ApplyOptions(new SelfDiagnosticsOptions { LogToStdout = false });
        Assert.False(logger.IsEnabled(LogLevel.Critical));

        logger.ApplyOptions(new SelfDiagnosticsOptions { LogToStdout = true });
        logger.Log(LogLevel.Warning, default, "after re-enable", null, static (m, _) => m);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("after re-enable", StringComparison.Ordinal)),
            "expected logging to resume after the console sink was re-enabled");
    }

    [Fact]
    public void PreambleFactory_NotInvokedWithoutFileSink()
    {
        var preambleCalls = 0;
        string CountingPreamble(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
        {
            preambleCalls++;
            return "preamble";
        }

        using var stdout = new SynchronizedStringWriter();
        var options = new SelfDiagnosticsOptions { LogToStdout = true };
        var manager = new SelfDiagnosticsSinkManager(
            CountingPreamble,
            static _ => { },
            stdoutWriter: () => stdout,
            stderrWriter: () => stdout);

        using var logger = new SelfDiagnosticsLogger(options, CountingPreamble, manager);

        logger.Log(LogLevel.Warning, default, "console only", null, static (m, _) => m);

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("console only", StringComparison.Ordinal)),
            "expected the console entry to reach stdout without invoking the preamble factory");
        Assert.Equal(0, preambleCalls);
    }

    [Fact]
    public void EventListener_PreservesEventIdAndName()
    {
        using var sink = new TestSink();
        using var dispatcher = new SelfDiagnosticsSinkDispatcher(sinkResolver: _ => [sink]);
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        using var applied = new ManualResetEventSlim(false);
        dispatcher.QueueConfiguration(
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(
                new SelfDiagnosticsOptions { LogToStdout = true, MinimumLevel = LogLevel.Warning }),
            1,
            (_, _, _) => applied.Set());
        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        Assert.True(applied.Wait(TimeSpan.FromSeconds(5)), "Configuration was not applied by the pump within the timeout");
        using var eventSource = new TestEventSource();
        eventSource.DiagnosticEvent("event payload");

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(
                () => sink.Written.Any(item => item.Entry.EventId.Id == 42)),
            "expected the EventSource event to reach the sink with EventId 42");
        var entry = Assert.Single(sink.Written, item => item.Entry.EventId.Id == 42).Entry;
        Assert.Equal("DiagnosticEvent", entry.EventId.Name);
    }

    [Fact]
    public void ApplyOptions_CreatesFileSinkOffCallingThread()
    {
        using var directory = new TemporaryDirectory();

        var callingThreadId = Environment.CurrentManagedThreadId;
        var preambleThreadId = 0;
        string CapturePreambleThread(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
        {
            Volatile.Write(ref preambleThreadId, Environment.CurrentManagedThreadId);
            return string.Empty;
        }

        var manager = new SelfDiagnosticsSinkManager(CapturePreambleThread, static _ => { });
        using var logger = new SelfDiagnosticsLogger(new SelfDiagnosticsOptions(), CapturePreambleThread, manager);

        logger.ApplyOptions(new SelfDiagnosticsOptions { LogDirectory = directory.Path });

        Assert.True(
            SelfDiagnosticsTestHelpers.WaitUntil(() => Volatile.Read(ref preambleThreadId) != 0),
            "preamble factory was never invoked on the dispatcher pump thread");
        Assert.NotEqual(callingThreadId, Volatile.Read(ref preambleThreadId));
    }

    private static SelfDiagnosticsLogger CreateConsoleLogger(
        SelfDiagnosticsOptions options,
        SynchronizedStringWriter stdout,
        bool startImmediately = true)
    {
        var manager = new SelfDiagnosticsSinkManager(
            static _ => string.Empty,
            static _ => { },
            stdoutWriter: () => stdout,
            stderrWriter: () => stdout);

        return new SelfDiagnosticsLogger(
            options,
            static _ => string.Empty,
            manager,
            startImmediately: startImmediately);
    }

    [EventSource(Name = "OpenTelemetry-SelfDiagnosticsLoggerTests")]
    private sealed class TestEventSource : EventSource
    {
        [Event(42, Level = EventLevel.Warning, Message = "{0}")]
        public void DiagnosticEvent(string message) => this.WriteEvent(42, message);
    }
}
