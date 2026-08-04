// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsAssemblyLoggerTests
{
    [Fact]
    public void Construction_DoesNotScan_EvenWhenDebugEnabled()
    {
        var recording = new RecordingLogger();

        using var assemblyLogger = new SelfDiagnosticsAssemblyLogger(recording);

        Assert.Empty(recording.Entries);
    }

    [Fact]
    public void Scan_ReportsLoadedAssembliesAtDebug()
    {
        var recording = new RecordingLogger();

        using var assemblyLogger = new SelfDiagnosticsAssemblyLogger(recording);
        assemblyLogger.TryLogLoadedAssemblies();

        Assert.Contains(recording.Entries, e => e.Message.StartsWith("Assembly loaded:", StringComparison.Ordinal));
        Assert.All(recording.Entries, e => Assert.Equal(LogLevel.Debug, e.Level));
    }

    [Fact]
    public void Rescan_IsIdempotent()
    {
        var recording = new RecordingLogger();

        using var assemblyLogger = new SelfDiagnosticsAssemblyLogger(recording);
        assemblyLogger.TryLogLoadedAssemblies();
        var afterFirstScan = recording.Entries.Count;
        Assert.True(afterFirstScan > 0);

        assemblyLogger.TryLogLoadedAssemblies();

        Assert.Equal(afterFirstScan, recording.Entries.Count);
    }

    [Fact]
    public void ScanSkipped_WhenDebugDisabled_ThenCapturedOnRescanAfterLevelRaise()
    {
        var recording = new RecordingLogger { MinimumLevel = LogLevel.Warning };

        using var assemblyLogger = new SelfDiagnosticsAssemblyLogger(recording);
        Assert.Empty(recording.Entries);

        recording.MinimumLevel = LogLevel.Debug; // simulates a remote config raising the level
        assemblyLogger.TryLogLoadedAssemblies();

        Assert.Contains(recording.Entries, e => e.Message.StartsWith("Assembly loaded:", StringComparison.Ordinal));
    }

    [Fact]
    public void Dispose_StopsLogging()
    {
        var recording = new RecordingLogger();

        var assemblyLogger = new SelfDiagnosticsAssemblyLogger(recording);
        assemblyLogger.TryLogLoadedAssemblies();
        assemblyLogger.Dispose();

        var afterDispose = recording.Entries.Count;
        assemblyLogger.TryLogLoadedAssemblies();

        Assert.Equal(afterDispose, recording.Entries.Count);
    }

    [Fact]
    public void RuntimeInfrastructureAssemblies_AreSkipped()
    {
        var recording = new RecordingLogger();

        using var assemblyLogger = new SelfDiagnosticsAssemblyLogger(recording);
        assemblyLogger.TryLogLoadedAssemblies();

        Assert.DoesNotContain(recording.Entries, e => e.Message.Contains("Assembly loaded: System.Private.CoreLib,", StringComparison.Ordinal));
        Assert.DoesNotContain(recording.Entries, e => e.Message.Contains("Assembly loaded: mscorlib,", StringComparison.Ordinal));
    }
}
