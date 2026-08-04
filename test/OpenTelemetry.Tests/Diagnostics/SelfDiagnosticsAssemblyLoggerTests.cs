// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Diagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public class SelfDiagnosticsAssemblyLoggerTests
{
    [Fact]
    public void Construction_DoesNotScan_EvenWhenDebugEnabled()
    {
        // Regression: the constructor used to scan every loaded assembly. It runs while the
        // caller holds the process-global self-diagnostics locks, so the sweep is driven from
        // SelfDiagnostics.OnConfigurationApplied (off-lock) instead.
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
        // Regression: a remotely raised level triggers a re-scan; the dedup set must prevent
        // duplicate entries for assemblies already reported.
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
        // Regression: with the static design, an incident-time level raise (e.g. via OpAMP)
        // could never capture the assemblies loaded before the raise. The re-scan hook must.
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
