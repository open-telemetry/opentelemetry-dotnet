// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Diagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Self diagnostics class captures the EventSource events sent by OpenTelemetry
/// modules and writes them to local file for internal troubleshooting.
/// </summary>
internal sealed class SelfDiagnostics : IDisposable
{
    /// <summary>
    /// Long-living object that holds relevant resources.
    /// </summary>
    private static readonly SelfDiagnostics Instance = new();

    private static readonly Lock LazyInitLock = new();
    private static readonly SelfDiagnosticsOptions.SelfDiagnosticsConfigurationCoordinator ConfigurationCoordinator = new(ApplyConfiguration);

    private static volatile SelfDiagnosticsLogger? sdkLogger;
    private static SelfDiagnosticsLoggingEventListener? sdkListener;
    private static SelfDiagnosticsAssemblyLogger? sdkAssemblyLogger;
    private static long latestConfigurationGeneration;

    private readonly SelfDiagnosticsConfigRefresher configRefresher;

    static SelfDiagnostics()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Instance.Dispose();
    }

    private SelfDiagnostics()
    {
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
        ConfigurationCoordinator.Dispose();
        this.configRefresher.Dispose();

        SelfDiagnosticsLogger? logger;
        SelfDiagnosticsLoggingEventListener? listener;
        SelfDiagnosticsAssemblyLogger? assemblyLogger;

        lock (LazyInitLock)
        {
            listener = sdkListener;
            assemblyLogger = sdkAssemblyLogger;
            logger = sdkLogger;
            sdkListener = null;
            sdkAssemblyLogger = null;
            sdkLogger = null;
        }

        listener?.Dispose();
        assemblyLogger?.Dispose();
        logger?.Dispose();
    }

    /// <summary>
    /// Registers a provider's live self-diagnostics options. The most recently registered live
    /// provider owns the process-global configuration until its returned lease is disposed.
    /// </summary>
    /// <param name="monitor">The provider's live self-diagnostics options.</param>
    /// <returns>A lease that relinquishes ownership when the provider is disposed.</returns>
    internal static IDisposable Initialize(IOptionsMonitor<SelfDiagnosticsOptions> monitor)
        => ConfigurationCoordinator.Register(monitor);

    private static void ApplyConfiguration(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        lock (LazyInitLock)
        {
            var generation = ++latestConfigurationGeneration;

            if (sdkLogger is null)
            {
                if (configuration.MinimumLevel != LogLevel.None && configuration.HasConfiguredSink)
                {
                    CreateLoggingStack(configuration, generation);
                }

                return;
            }

            sdkLogger.ApplyConfiguration(configuration, generation);
        }
    }

    // Must be called under LazyInitLock (and, via the coordinator, its sync lock).
    // The EventListener is constructed here rather than deferred to the dispatcher pump: its
    // base constructor is what subscribes to the already-registered OpenTelemetry-* sources, so
    // deferring it would blind the window between provider construction and the pump's first
    // pass. Everything else that could run long is kept off this path - sink creation happens on
    // the pump, and SelfDiagnosticsAssemblyLogger only subscribes here, with its scan driven
    // from OnConfigurationApplied outside the lock.
    private static void CreateLoggingStack(
        SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration,
        long generation)
    {
        var logger = new SelfDiagnosticsLogger(
            configuration,
            SelfDiagnosticsPreamble.Build,
            initialGeneration: generation,
            startImmediately: false);

        var listener = new SelfDiagnosticsLoggingEventListener(logger, configuration.EffectiveLevel);
        var assemblyLogger = new SelfDiagnosticsAssemblyLogger(logger);

        logger.ConfigurationApplied = (appliedGeneration, hasConfiguredSink, appliedConfiguration) =>
            OnConfigurationApplied(
                logger,
                listener,
                assemblyLogger,
                appliedGeneration,
                hasConfiguredSink,
                appliedConfiguration);

        sdkLogger = logger;
        sdkListener = listener;
        sdkAssemblyLogger = assemblyLogger;

        logger.ApplyConfiguration(configuration, generation);
    }

    private static void OnConfigurationApplied(
        SelfDiagnosticsLogger logger,
        SelfDiagnosticsLoggingEventListener listener,
        SelfDiagnosticsAssemblyLogger assemblyLogger,
        long generation,
        bool hasConfiguredSink,
        SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        bool logLoadedAssemblies;

        lock (LazyInitLock)
        {
            if (!ReferenceEquals(sdkLogger, logger)
                || generation != latestConfigurationGeneration)
            {
                return;
            }

            var effectiveLevel = hasConfiguredSink
                ? configuration.EffectiveLevel
                : LogLevel.None;
            listener.UpdateLevel(effectiveLevel);
            logLoadedAssemblies = effectiveLevel <= LogLevel.Debug;
        }

        if (logLoadedAssemblies)
        {
            assemblyLogger.TryLogLoadedAssemblies();
        }
    }
}
