// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Captures SDK self-diagnostics and serializes sink configuration, writes, and disposal through
/// <see cref="SelfDiagnosticsSinkDispatcher"/>.
/// </summary>
/// <remarks>
/// Entries captured before the first configuration is applied are buffered by the dispatcher,
/// because sink construction runs on the pump rather than on the calling thread. Configuration
/// callbacks run after the dispatcher has installed the corresponding sink set.
/// </remarks>
internal sealed class SelfDiagnosticsLogger : IDisposable, ILogger
{
    private readonly Lock updateLock = new();
    private long nextGeneration;
    private long latestQueuedGeneration = -1;
    private bool disposed;

    internal SelfDiagnosticsLogger(
        SelfDiagnosticsOptions options,
        Func<SelfDiagnosticsOptions.SelfDiagnosticsConfiguration, string> preambleBuilder,
        SelfDiagnosticsSinkManager? sinkManager = null,
        SelfDiagnosticsSinkDispatcher? dispatcher = null,
        long initialGeneration = 0,
        bool startImmediately = true)
        : this(
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(options),
            preambleBuilder,
            sinkManager,
            dispatcher,
            initialGeneration,
            startImmediately)
    {
    }

    internal SelfDiagnosticsLogger(
        SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration,
        Func<SelfDiagnosticsOptions.SelfDiagnosticsConfiguration, string> preambleBuilder,
        SelfDiagnosticsSinkManager? sinkManager = null,
        SelfDiagnosticsSinkDispatcher? dispatcher = null,
        long initialGeneration = 0,
        bool startImmediately = true)
    {
        this.Dispatcher = dispatcher ?? new();
        sinkManager ??= new(preambleBuilder, this.Dispatcher.ReportInternalError);
        this.Dispatcher.SetSinkManager(sinkManager);

        this.nextGeneration = initialGeneration;
        var generation = initialGeneration > 0
            ? initialGeneration
            : ++this.nextGeneration;

        if (startImmediately)
        {
            this.ApplyConfiguration(configuration, generation);
        }
        else
        {
            // The caller applies the configuration itself once the rest of the stack is wired.
            // Until then the dispatcher captures at the configured level and buffers.
            this.Dispatcher.PreparePending(configuration);
        }
    }

    /// <summary>
    /// Gets or sets a callback invoked on the dispatcher thread after a configuration generation
    /// has been applied to the sink set.
    /// </summary>
    internal Action<long, bool, SelfDiagnosticsOptions.SelfDiagnosticsConfiguration>? ConfigurationApplied { get; set; }

    /// <summary>Gets the dispatcher. Exposed for tests.</summary>
    internal SelfDiagnosticsSinkDispatcher Dispatcher { get; }

    public bool IsEnabled(LogLevel logLevel) => this.Dispatcher.IsEnabled(logLevel);

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!this.Dispatcher.IsEnabled(logLevel))
        {
            return;
        }

        var entry = SelfDiagnosticsLogEntry.Capture(logLevel, eventId, formatter(state, exception), exception);
        this.Dispatcher.Enqueue(in entry);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => NullScope.Instance;

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (this.updateLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
        }

        this.Dispatcher.Dispose();
    }

    internal void Write(in SelfDiagnosticsLogEntry entry) => this.Dispatcher.Enqueue(in entry);

    internal void ApplyOptions(SelfDiagnosticsOptions options)
        => this.ApplyConfiguration(
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration.Create(options),
            Interlocked.Increment(ref this.nextGeneration));

    internal void ApplyConfiguration(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration, long generation)
    {
        lock (this.updateLock)
        {
            if (this.disposed || generation <= this.latestQueuedGeneration)
            {
                return;
            }

            this.latestQueuedGeneration = generation;

            this.Dispatcher.QueueConfiguration(
                configuration,
                generation,
                this.OnConfigurationApplied);
        }
    }

    private void OnConfigurationApplied(
        long generation,
        bool hasConfiguredSink,
        SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        try
        {
            this.ConfigurationApplied?.Invoke(generation, hasConfiguredSink, configuration);
        }
        catch
        {
            // The dispatcher must survive owner lifecycle callback failures.
        }
    }

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
