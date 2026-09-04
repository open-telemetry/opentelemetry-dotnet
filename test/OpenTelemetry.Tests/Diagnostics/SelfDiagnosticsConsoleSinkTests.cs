// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsConsoleSinkTests
{
    [Fact]
    public void StdoutOnly_AllLevelsGoToStdout()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var sink = new SelfDiagnosticsConsoleSink(logToStdout: true, logToStderr: false, () => stdout, () => stderr);

        Write(sink, LogLevel.Debug, "debug line");
        Write(sink, LogLevel.Critical, "critical line");

        Assert.Contains("debug line", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("critical line", stdout.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public void StderrOnly_AllLevelsGoToStderr()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var sink = new SelfDiagnosticsConsoleSink(logToStdout: false, logToStderr: true, () => stdout, () => stderr);

        Write(sink, LogLevel.Debug, "debug line");
        Write(sink, LogLevel.Critical, "critical line");

        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("debug line", stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("critical line", stderr.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LogLevel.Trace, false)]
    [InlineData(LogLevel.Debug, false)]
    [InlineData(LogLevel.Information, false)]
    [InlineData(LogLevel.Warning, false)]
    [InlineData(LogLevel.Error, true)]
    [InlineData(LogLevel.Critical, true)]
    public void BothStreams_SplitAtWarningBoundary(LogLevel level, bool expectStderr)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var sink = new SelfDiagnosticsConsoleSink(logToStdout: true, logToStderr: true, () => stdout, () => stderr);

        Write(sink, level, "the line");

        var target = expectStderr ? stderr : stdout;
        var other = expectStderr ? stdout : stderr;
        Assert.Contains("the line", target.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, other.ToString());
    }

    [Fact]
    public void UpdateConsoleFlags_RetargetsRouting()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var sink = new SelfDiagnosticsConsoleSink(logToStdout: true, logToStderr: false, () => stdout, () => stderr);

        sink.UpdateConsoleFlags(logToStdout: false, logToStderr: true);
        Write(sink, LogLevel.Warning, "after retarget");

        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("after retarget", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IsEnabled_FalseWhenBothStreamsDisabled()
    {
        using var sink = new SelfDiagnosticsConsoleSink(logToStdout: true, logToStderr: false);

        Assert.True(sink.IsEnabled(LogLevel.Warning));

        sink.UpdateConsoleFlags(logToStdout: false, logToStderr: false);

        Assert.False(sink.IsEnabled(LogLevel.Warning));
    }

    [Fact]
    public void Write_FailingWriter_DoesNotThrow()
    {
        using var sink = new SelfDiagnosticsConsoleSink(
            logToStdout: true,
            logToStderr: false,
            static () => throw new IOException("broken pipe"));

        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, "message", null);
        sink.Write(in entry, "message"); // must not propagate into the pump
    }

    private static void Write(SelfDiagnosticsConsoleSink sink, LogLevel level, string message)
    {
        var entry = SelfDiagnosticsLogEntry.Capture(level, default, message, null);
        sink.Write(in entry, message);
    }
}
