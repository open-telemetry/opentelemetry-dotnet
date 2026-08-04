// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Internal;

/// <summary>
/// Owns the self-diagnostics logging stack.
/// </summary>
/// <remarks>
/// <para>
/// This type holds no static state. The process-wide singleton lives in
/// <see cref="SelfDiagnostics"/>, which owns exactly one instance of this
/// class; keeping the two concerns separate means the stack's lifecycle can be exercised in
/// isolation, without a test permanently installing an <see cref="EventListener"/>
/// and a background pump thread into the host process.
/// </para>
/// <para>
/// The stack is created lazily. Until some registered provider asks for a level and a sink,
/// nothing is constructed and the SDK stays silent. Once created it is retained for the lifetime
/// of the controller, because the listener's subscription to the <c>OpenTelemetry-*</c> sources is
/// what makes later reconfiguration possible. A configuration that resolves to no sink drops the
/// sink set and disables event delivery rather than tearing the stack down.
/// </para>
/// </remarks>
internal sealed class SelfDiagnosticsController : IDisposable
{
    private readonly Lock stateLock = new();
    private readonly SelfDiagnosticsOptions.SelfDiagnosticsConfigurationCoordinator coordinator;

    private volatile SelfDiagnosticsLogger? logger;
    private SelfDiagnosticsLoggingEventListener? listener;
    private SelfDiagnosticsAssemblyLogger? assemblyLogger;
    private long latestConfigurationGeneration;
    private bool disposed;

    internal SelfDiagnosticsController()
    {
        this.coordinator =
            new SelfDiagnosticsOptions.SelfDiagnosticsConfigurationCoordinator(this.ApplyConfiguration);
    }

    /// <summary>
    /// Gets the active logger, or <see langword="null"/> before the stack is created. Exposed for tests.
    /// </summary>
    internal SelfDiagnosticsLogger? Logger => this.logger;

    /// <inheritdoc/>
    public void Dispose()
    {
        this.coordinator.Dispose();

        SelfDiagnosticsLogger? logger;
        SelfDiagnosticsLoggingEventListener? listener;
        SelfDiagnosticsAssemblyLogger? assemblyLogger;

        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            listener = this.listener;
            assemblyLogger = this.assemblyLogger;
            logger = this.logger;
            this.listener = null;
            this.assemblyLogger = null;
            this.logger = null;
        }

        listener?.Dispose();
        assemblyLogger?.Dispose();
        logger?.Dispose();
    }

    /// <summary>
    /// Registers a provider's live self-diagnostics options. The most recently registered provider
    /// that configured a sink owns the configuration until its returned lease is disposed.
    /// </summary>
    /// <param name="monitor">The provider's live self-diagnostics options.</param>
    /// <returns>A lease that relinquishes ownership when the provider is disposed.</returns>
    internal IDisposable Register(IOptionsMonitor<SelfDiagnosticsOptions> monitor)
        => this.coordinator.Register(monitor);

    private void ApplyConfiguration(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }

            var generation = ++this.latestConfigurationGeneration;

            if (this.logger is null)
            {
                if (configuration.MinimumLevel != LogLevel.None && configuration.HasConfiguredSink)
                {
                    this.CreateLoggingStackUnderLock(configuration, generation);
                }

                return;
            }

            this.logger.ApplyConfiguration(configuration, generation);

            // QueueConfiguration publishes the new dispatcher gate synchronously. Re-enable (or
            // disable) existing EventSources before returning so provider-construction events do
            // not fall into the asynchronous gap before the pump applies this generation.
            this.listener!.UpdateLevel(configuration.EffectiveLevel);
        }
    }

    // Must be called under stateLock (and, via the coordinator, its sync lock).
    // The EventListener is constructed here rather than deferred to the dispatcher pump: its
    // base constructor is what subscribes to the already-registered OpenTelemetry-* sources, so
    // deferring it would blind the window between provider construction and the pump's first
    // pass. Everything else that could run long is kept off this path - sink creation happens on
    // the pump, and SelfDiagnosticsAssemblyLogger only subscribes here, with its scan driven
    // from OnConfigurationApplied outside the lock.
    private void CreateLoggingStackUnderLock(
        SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration,
        long generation)
    {
        var newLogger = new SelfDiagnosticsLogger(
            configuration,
            SelfDiagnosticsPreamble.Build,
            initialGeneration: generation,
            startImmediately: false);

        var newListener = new SelfDiagnosticsLoggingEventListener(newLogger, configuration.EffectiveLevel);
        var newAssemblyLogger = new SelfDiagnosticsAssemblyLogger(newLogger);

        newLogger.ConfigurationApplied = (appliedGeneration, hasConfiguredSink, appliedConfiguration) =>
            this.OnConfigurationApplied(
                newLogger,
                newListener,
                newAssemblyLogger,
                appliedGeneration,
                hasConfiguredSink,
                appliedConfiguration);

        this.logger = newLogger;
        this.listener = newListener;
        this.assemblyLogger = newAssemblyLogger;

        newLogger.ApplyConfiguration(configuration, generation);
    }

    private void OnConfigurationApplied(
        SelfDiagnosticsLogger logger,
        SelfDiagnosticsLoggingEventListener listener,
        SelfDiagnosticsAssemblyLogger assemblyLogger,
        long generation,
        bool hasConfiguredSink,
        SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        bool logLoadedAssemblies;

        lock (this.stateLock)
        {
            if (this.disposed
                || !ReferenceEquals(this.logger, logger)
                || generation != this.latestConfigurationGeneration)
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
