// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// Self diagnostics class captures the EventSource events sent by OpenTelemetry
/// modules and writes them to local file for internal troubleshooting.
/// </summary>
internal sealed class SelfDiagnostics : IDisposable
{
    /// <summary>
    /// Long-living object that hold relevant resources.
    /// </summary>
    private static readonly SelfDiagnostics Instance = new();
    private readonly SelfDiagnosticsConfigRefresher configRefresher;

    static SelfDiagnostics()
    {
        AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
        {
            Instance.Dispose();
        };
    }

    private SelfDiagnostics()
    {
        this.configRefresher = new SelfDiagnosticsConfigRefresher();
    }

    /// <summary>
    /// No member of SelfDiagnostics class is explicitly called when an EventSource class, say
    /// OpenTelemetryApiEventSource, is invoked to send an event.
    /// To trigger CLR to initialize static fields and static constructors of SelfDiagnostics,
    /// call EnsureInitialized method before any EventSource event is sent.
    /// </summary>
    public static void EnsureInitialized()
    {
    }

    /// <inheritdoc/>
    public void Dispose() =>
        this.configRefresher.Dispose();
}
