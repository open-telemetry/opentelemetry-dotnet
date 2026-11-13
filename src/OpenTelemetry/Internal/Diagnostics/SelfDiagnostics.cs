// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Process-wide holder for SDK self-diagnostics.
/// </summary>
/// <remarks>
/// This type exists only to own the singletons and to tear them down on process exit. The logic
/// lives in <see cref="SelfDiagnosticsController"/>. The legacy
/// <c>OTEL_DIAGNOSTICS.json</c> file supplies options to the same pipeline through
/// <see cref="SelfDiagnosticsLegacyConfigSource"/>.
/// </remarks>
#if NET
[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
#endif
internal sealed class SelfDiagnostics : IDisposable
{
    /// <summary>
    /// Long-living object that holds relevant resources.
    /// </summary>
    private static readonly SelfDiagnostics Instance = new();

    private readonly SelfDiagnosticsController controller;
    private readonly SelfDiagnosticsLegacyConfigSource legacySource;

    static SelfDiagnostics()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Instance.Dispose();
    }

    internal SelfDiagnostics(SelfDiagnosticsConfigParser legacyConfigParser, TimeSpan? legacyPollInterval)
    {
        this.controller = new SelfDiagnosticsController(
            static configFilePath => OpenTelemetrySdkEventSource.Log.SelfDiagnosticsLegacyConfigIgnored(configFilePath));
        this.legacySource = new SelfDiagnosticsLegacyConfigSource(
            legacyConfigParser,
            legacyPollInterval,
            this.controller.SetLegacyConfiguration);
    }

    private SelfDiagnostics()
        : this(new SelfDiagnosticsConfigParser(), null)
    {
    }

    /// <summary>
    /// Triggers CLR initialization before an EventSource event is emitted.
    /// </summary>
    public static void EnsureInitialized()
    {
        // Empty body; invoking this method forces type initialization of the process-wide
        // singleton (static constructor, ProcessExit hook, and controller).
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Controller first: a poll racing with shutdown then finds the coordinator disposed
        // instead of re-applying configuration while the pipeline is being torn down.
        this.controller.Dispose();
        this.legacySource.Dispose();
    }

    /// <summary>
    /// Registers a provider's live self-diagnostics options. The most recently registered
    /// provider that configured a sink owns the process-global configuration until its returned
    /// lease is disposed.
    /// </summary>
    /// <param name="monitor">The provider's live self-diagnostics options.</param>
    /// <returns>A lease that relinquishes ownership when the provider is disposed.</returns>
    internal static IDisposable Initialize(IOptionsMonitor<SelfDiagnosticsOptions> monitor)
        => Instance.Register(monitor);

    /// <inheritdoc cref="Initialize"/>
    internal IDisposable Register(IOptionsMonitor<SelfDiagnosticsOptions> monitor)
        => this.controller.Register(monitor);
}
