// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
#if NET
using System.Runtime.CompilerServices;
#endif
using Microsoft.Extensions.Logging;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Internal;

/// <summary>
/// Logs loaded assemblies to the self-diagnostics logger: subsequent loads are logged as they
/// happen, and already-loaded assemblies are captured when <see cref="TryLogLoadedAssemblies"/>
/// is called.
/// </summary>
/// <remarks>
/// <para>
/// Entries are emitted at <see cref="LogLevel.Debug"/>. Because a scan is skipped
/// when Debug is not enabled, <see cref="TryLogLoadedAssemblies"/> should be called again
/// whenever the effective level is raised to Debug or below (e.g. by remote configuration
/// during an incident). The per-instance dedup set makes re-scans idempotent, so callers
/// never produce duplicates.
/// </para>
/// </remarks>
internal sealed class SelfDiagnosticsAssemblyLogger : IDisposable
{
    // Assemblies with no independent diagnostic value - their identity is already captured
    // by the runtime version string, or they are pure infrastructure shims.
    private static readonly string[] SkipByName =
    {
        "mscorlib",
        "netstandard",
        "System.Private.CoreLib",
        "dotnet",
    };

    private readonly ILogger logger;
    private readonly AssemblyLoadEventHandler assemblyLoadHandler;

    // Key: "{name}|{assemblyVersion}" - version is included so that distinct versions of the
    // same library loaded into separate AssemblyLoadContexts are each captured. Keying on the
    // CLR binding version (not informational version) matches the identity the runtime uses.
    private readonly ConcurrentDictionary<string, byte> logged = new(StringComparer.OrdinalIgnoreCase);

    private volatile bool disposed;

    internal SelfDiagnosticsAssemblyLogger(ILogger logger)
    {
        this.logger = logger;

        // Subscribe now so that no assembly loaded before the caller's first
        // TryLogLoadedAssemblies is missed; the dedup set suppresses the overlap.
        this.assemblyLoadHandler = (_, args) => this.TryLog(args.LoadedAssembly);
        AppDomain.CurrentDomain.AssemblyLoad += this.assemblyLoadHandler;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.disposed = true;
        AppDomain.CurrentDomain.AssemblyLoad -= this.assemblyLoadHandler;
    }

    /// <summary>
    /// Scans all currently-loaded assemblies and logs any not yet reported. No-op when the
    /// logger does not accept <see cref="LogLevel.Debug"/>.
    /// Idempotent; safe to call whenever the effective level changes.
    /// </summary>
    internal void TryLogLoadedAssemblies()
    {
        if (this.disposed || !this.logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            this.TryLog(assembly);
        }
    }

#if NET
    [UnconditionalSuppressMessage(
        "SingleFile",
        "IL3000:AvoidAssemblyLocationInSingleFile",
        Justification = "Assembly.Location is only accessed when RuntimeFeature.IsDynamicCodeSupported is true. In AoT/single-file scenarios the location is omitted and an empty string is used instead.")]
#endif
    private void TryLog(Assembly assembly)
    {
        if (this.disposed || !this.logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        try
        {
            var assemblyName = assembly.GetName();
            var name = assemblyName.Name;
            if (string.IsNullOrEmpty(name) || SkipByName.Contains(name))
            {
                return;
            }

            var assemblyVersion = assemblyName.Version?.ToString() ?? "unknown";
            if (!this.logged.TryAdd($"{name}|{assemblyVersion}", 0))
            {
                return;
            }

            // InformationalVersion carries the full semver including pre-release labels and
            // git commit SHA for official packages. AssemblyVersion is also logged because it
            // is often locked to a major version for binary compatibility and can differ
            // significantly from the actual release version.
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

#if NET
            var location = RuntimeFeature.IsDynamicCodeSupported ? assembly.Location : string.Empty;
#else
            var location = assembly.IsDynamic ? string.Empty : assembly.Location;
#endif
            var directory = string.IsNullOrEmpty(location)
                ? "(location unavailable)"
                : Path.GetDirectoryName(location) ?? "(location unavailable)";

            var message = informationalVersion is not null
                ? $"Assembly loaded: {name}, AssemblyVersion={assemblyVersion}, InformationalVersion={informationalVersion}, Directory={directory}"
                : $"Assembly loaded: {name}, AssemblyVersion={assemblyVersion}, Directory={directory}";

            this.logger.Log(LogLevel.Debug, default, message, null, static (m, _) => m);
        }
        catch
        {
            // A failure introspecting a single assembly must not disrupt diagnostics.
        }
    }
}
