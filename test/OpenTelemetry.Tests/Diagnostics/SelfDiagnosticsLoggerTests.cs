// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsLoggerTests
{
    [Fact]
    public void NonDeferred_LogFlowsToConsoleSink()
    {
        using var stdout = new SynchronizedStringWriter();
        using var logger = CreateConsoleLogger(new SelfDiagnosticsOptions { LogToStdout = true }, stdout);

        logger.Log(LogLevel.Warning, default, "hello diagnostics", null, static (m, _) => m);

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("hello diagnostics", StringComparison.Ordinal)));
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

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("should appear", StringComparison.Ordinal)));
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

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("buffered warning", StringComparison.Ordinal)));
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

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => callbackHasSinks.HasValue));
        Assert.False(callbackHasSinks);
        Assert.False(logger.IsEnabled(LogLevel.Critical)); // active with zero sinks: nothing is enabled
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

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("after re-enable", StringComparison.Ordinal)));
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

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => stdout.ToString().Contains("console only", StringComparison.Ordinal)));
        Assert.Equal(0, preambleCalls);
    }

    [Fact]
    public void EventListener_PreservesEventIdAndName()
    {
        using var dispatcher = new SelfDiagnosticsSinkDispatcher();
        using var sink = new TestSink();
        using var logger = new SelfDiagnosticsLogger(
            new SelfDiagnosticsOptions(),
            static _ => string.Empty,
            dispatcher: dispatcher,
            startImmediately: false);
        Assert.True(dispatcher.Activate([sink], LogLevel.Warning));

        using var listener = new SelfDiagnosticsLoggingEventListener(logger, LogLevel.Warning);
        using var eventSource = new TestEventSource();
        eventSource.DiagnosticEvent("event payload");

        Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(
            () => sink.Written.Any(item => item.Entry.EventId.Id == 42)));
        var entry = Assert.Single(sink.Written, item => item.Entry.EventId.Id == 42).Entry;
        Assert.Equal("DiagnosticEvent", entry.EventId.Name);
    }

    [Fact]
    public void ApplyOptions_CreatesFileSinkOffCallingThread()
    {
        var directory = Path.Combine(Path.GetTempPath(), "otel-selfdiag-dispatcher-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var callingThreadId = Environment.CurrentManagedThreadId;
            var preambleThreadId = 0;
            string CapturePreambleThread(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
            {
                Volatile.Write(ref preambleThreadId, Environment.CurrentManagedThreadId);
                return string.Empty;
            }

            var manager = new SelfDiagnosticsSinkManager(CapturePreambleThread, static _ => { });
            using var logger = new SelfDiagnosticsLogger(new SelfDiagnosticsOptions(), CapturePreambleThread, manager);

            logger.ApplyOptions(new SelfDiagnosticsOptions { LogDirectory = directory });

            Assert.True(SelfDiagnosticsTestHelpers.WaitUntil(() => Volatile.Read(ref preambleThreadId) != 0));
            Assert.NotEqual(callingThreadId, Volatile.Read(ref preambleThreadId));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
