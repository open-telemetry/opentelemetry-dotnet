// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace OpenTelemetry.Internal;

/// <summary>
/// Process-wide holder for SDK self-diagnostics.
/// </summary>
/// <remarks>
/// This type exists only to own the singletons and to tear them down on process exit. The logic
/// lives in <see cref="SelfDiagnosticsController"/> (the current mechanism) and
/// <see cref="SelfDiagnosticsConfigRefresher"/> (the legacy <c>OTEL_DIAGNOSTICS.json</c>
/// mechanism, retained for backwards compatibility).
/// </remarks>
internal sealed class SelfDiagnostics : IDisposable
{
    /// <summary>
    /// Long-living object that holds relevant resources.
    /// </summary>
    private static readonly SelfDiagnostics Instance = new();

    private readonly SelfDiagnosticsController controller;
    private readonly SelfDiagnosticsConfigRefresher configRefresher;

    static SelfDiagnostics()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Instance.Dispose();
    }

    private SelfDiagnostics()
    {
        this.controller = new SelfDiagnosticsController();
        this.configRefresher = new SelfDiagnosticsConfigRefresher();
    }

    /// <summary>
    /// Triggers CLR initialization before an EventSource event is emitted.
    /// </summary>
    public static void EnsureInitialized()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.controller.Dispose();
        this.configRefresher.Dispose();
    }

    /// <summary>
    /// Registers a provider's live self-diagnostics options. The most recently registered provider
    /// that configured a sink owns the process-global configuration until its returned lease is
    /// disposed.
    /// </summary>
    /// <param name="monitor">The provider's live self-diagnostics options.</param>
    /// <returns>A lease that relinquishes ownership when the provider is disposed.</returns>
    internal static IDisposable Initialize(IOptionsMonitor<SelfDiagnosticsOptions> monitor)
        => Instance.controller.Register(monitor);
}
