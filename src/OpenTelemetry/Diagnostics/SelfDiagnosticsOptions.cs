// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

// Aliased so XML doc crefs resolve: net462's mscorlib declares an internal System.LogLevel.
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Diagnostics;

/// <summary>
/// Options for configuring SDK self-diagnostics logging.
/// </summary>
/// <remarks>
/// <para>
/// By default all sinks are disabled (silent). Enable one or more sinks to receive
/// SDK self-diagnostics output. The options instance is reloadable - changes pushed
/// through <c>IOptionsMonitor&lt;SelfDiagnosticsOptions&gt;</c> take effect at runtime
/// without rebuilding the provider.
/// </para>
/// <para>
/// Environment variable defaults are applied before any <c>Configure&lt;SelfDiagnosticsOptions&gt;</c>
/// callbacks, but only when <c>OTEL_DOTNET_AUTO_HOME</c> is <b>not</b> set:
/// <list type="bullet">
///   <item><c>OTEL_LOG_LEVEL</c> sets <see cref="MinimumLevel"/>.</item>
///   <item><c>OTEL_DOTNET_AUTO_LOG_DIRECTORY</c> sets <see cref="LogDirectory"/>.</item>
/// </list>
/// When <c>OTEL_DOTNET_AUTO_HOME</c> is set the .NET auto-instrumentation agent handles
/// SDK EventSource logging via its own listener; applying the same env vars here would
/// produce duplicate log output. Explicit <c>Configure&lt;SelfDiagnosticsOptions&gt;</c>
/// calls always take effect regardless.
/// </para>
/// </remarks>
public sealed class SelfDiagnosticsOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SelfDiagnosticsOptions"/> class.
    /// </summary>
    public SelfDiagnosticsOptions()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfDiagnosticsOptions"/> class,
    /// reading environment-variable defaults from the provided <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The configuration source used to read OTEL_ environment variable defaults.</param>
    internal SelfDiagnosticsOptions(IConfiguration configuration)
    {
        // Detect auto-instrumentation. OTEL_DOTNET_AUTO_HOME is set unconditionally by every
        // official launcher (instrument.sh, instrument.cmd, psm1, NuGet content files) in both
        // profiler and startup-hook-only modes.
        var autoInstrumentationHome = Environment.GetEnvironmentVariable("OTEL_DOTNET_AUTO_HOME");
        this.AutoInstrumentationDetected = !string.IsNullOrEmpty(autoInstrumentationHome);

        // Both env vars are suppressed when running under auto-instrumentation.
        // The agent reads these same variables and handles SDK EventSource logging
        // via its own SdkSelfDiagnosticsEventListener; applying them here too would
        // produce duplicate output. Explicit Configure<SelfDiagnosticsOptions> calls
        // are not subject to this suppression.
        if (!this.AutoInstrumentationDetected)
        {
            if (configuration.TryGetStringValue("OTEL_LOG_LEVEL", out var logLevelRaw)
                && TryParseOtelLogLevel(logLevelRaw, out var parsedLevel))
            {
                this.MinimumLevel = parsedLevel;
            }

            if (configuration.TryGetStringValue("OTEL_DOTNET_AUTO_LOG_DIRECTORY", out var logDir))
            {
                this.LogDirectory = logDir;
            }
        }
    }

    /// <summary>
    /// Gets or sets the minimum log level. Events below this level are discarded.
    /// Defaults to <see cref="LogLevel.Warning"/>.
    /// </summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Gets or sets the directory in which self-diagnostics log files are written.
    /// Setting this property enables the file sink. <see langword="null"/> or empty
    /// disables file logging.
    /// </summary>
    public string? LogDirectory { get; set; }

    /// <summary>
    /// Gets or sets the maximum size of a single log file in kilobytes before it rolls
    /// over to a new file. Defaults to 10240 (10 MiB). Files are never truncated;
    /// a new file is opened when the limit is reached.
    /// </summary>
    public int FileSizeLimitKilobytes { get; set; } = 10_240;

    /// <summary>
    /// Gets or sets the maximum number of rolling log files to retain.
    /// When a new file is opened the oldest is deleted if this limit would be exceeded.
    /// Defaults to 3.
    /// </summary>
    public int MaxRetainedFiles { get; set; } = 3;

    /// <summary>
    /// Gets or sets a value indicating whether SDK diagnostics are written to standard output.
    /// When only this flag is set all entries go to standard output. When both this flag and
    /// <see cref="LogToStderr"/> are set, entries at <see cref="LogLevel.Warning"/> and below go to
    /// standard output and entries above <see cref="LogLevel.Warning"/> go to standard error.
    /// </summary>
    public bool LogToStdout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether SDK diagnostics are written to standard error.
    /// When only this flag is set all entries go to standard error. When both this flag and
    /// <see cref="LogToStdout"/> are set, entries above <see cref="LogLevel.Warning"/> go to standard
    /// error and entries at <see cref="LogLevel.Warning"/> and below go to standard output.
    /// </summary>
    public bool LogToStderr { get; set; }

    /// <summary>
    /// Gets a value indicating whether <c>OTEL_DOTNET_AUTO_HOME</c> was detected during
    /// options construction, which suppresses env-var-driven defaults.
    /// </summary>
    internal bool AutoInstrumentationDetected { get; }

    /// <summary>
    /// Parses an OTEL_LOG_LEVEL string value into a <see cref="LogLevel"/>.
    /// </summary>
    /// <param name="value">The raw OTEL_LOG_LEVEL string (e.g. "warn", "debug").</param>
    /// <param name="level">The parsed <see cref="LogLevel"/> when the return value is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is a recognized level string; otherwise <see langword="false"/>.</returns>
    internal static bool TryParseOtelLogLevel(string value, out LogLevel level)
    {
        switch (value.ToUpperInvariant())
        {
            case "ERROR":
                level = LogLevel.Error;
                return true;
            case "WARN":
                level = LogLevel.Warning;
                return true;
            case "INFO":
                level = LogLevel.Information;
                return true;
            case "DEBUG":
                level = LogLevel.Debug;
                return true;
            case "TRACE":
                level = LogLevel.Trace;
                return true;
            case "NONE":
                level = LogLevel.None;
                return true;
            default:
                level = LogLevel.None;
                return false;
        }
    }

    /// <summary>
    /// Immutable, internally-consistent snapshot of self-diagnostics options.
    /// </summary>
    internal sealed class SelfDiagnosticsConfiguration
    {
        internal static readonly SelfDiagnosticsConfiguration Disabled = new(
            LogLevel.None,
            null,
            10_240,
            3,
            false,
            false);

        private SelfDiagnosticsConfiguration(
            LogLevel minimumLevel,
            string? logDirectory,
            int fileSizeLimitKilobytes,
            int maxRetainedFiles,
            bool logToStdout,
            bool logToStderr)
        {
            this.MinimumLevel = minimumLevel;
            this.LogDirectory = logDirectory;
            this.FileSizeLimitKilobytes = fileSizeLimitKilobytes;
            this.MaxRetainedFiles = maxRetainedFiles;
            this.LogToStdout = logToStdout;
            this.LogToStderr = logToStderr;
        }

        internal LogLevel MinimumLevel { get; }

        internal string? LogDirectory { get; }

        internal int FileSizeLimitKilobytes { get; }

        internal int MaxRetainedFiles { get; }

        internal bool LogToStdout { get; }

        internal bool LogToStderr { get; }

        internal bool HasConfiguredSink
            => !string.IsNullOrEmpty(this.LogDirectory)
                || this.LogToStdout
                || this.LogToStderr;

        internal LogLevel EffectiveLevel
            => this.MinimumLevel != LogLevel.None && this.HasConfiguredSink
                ? this.MinimumLevel
                : LogLevel.None;

        internal static SelfDiagnosticsConfiguration Create(SelfDiagnosticsOptions options)
        {
            Guard.ThrowIfNull(options);

            return new(
                options.MinimumLevel,
                options.LogDirectory,
                options.FileSizeLimitKilobytes,
                options.MaxRetainedFiles,
                options.LogToStdout,
                options.LogToStderr);
        }
    }

    /// <summary>
    /// Coordinates process-global configuration supplied by independently-owned providers.
    /// </summary>
    internal sealed class SelfDiagnosticsConfigurationCoordinator : IDisposable
    {
        private readonly Lock syncLock = new();
        private readonly List<Registration> registrations = [];
        private readonly Action<SelfDiagnosticsConfiguration> applyConfiguration;
        private bool disposed;

        internal SelfDiagnosticsConfigurationCoordinator(Action<SelfDiagnosticsConfiguration> applyConfiguration)
        {
            this.applyConfiguration = applyConfiguration;
        }

        public void Dispose()
        {
            Registration[] registrations;

            lock (this.syncLock)
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
                registrations = [.. this.registrations];
                this.registrations.Clear();

                foreach (var registration in registrations)
                {
                    registration.Registered = false;
                }
            }

            foreach (var registration in registrations)
            {
                registration.DisposeSubscription();
            }
        }

        internal IDisposable Register(
            Microsoft.Extensions.Options.IOptionsMonitor<SelfDiagnosticsOptions> monitor)
        {
            Guard.ThrowIfNull(monitor);

            var registration = new Registration(this, monitor);
            registration.Subscribe();

            lock (this.syncLock)
            {
                if (this.disposed)
                {
                    registration.DisposeSubscription();
                    throw new ObjectDisposedException(nameof(SelfDiagnosticsConfigurationCoordinator));
                }

                registration.Registered = true;
                this.registrations.Add(registration);
                this.applyConfiguration(registration.GetLatestConfiguration());
            }

            return registration;
        }

        private void ConfigurationChanged(Registration registration)
        {
            lock (this.syncLock)
            {
                if (!this.disposed
                    && registration.Registered
                    && ReferenceEquals(this.registrations[this.registrations.Count - 1], registration))
                {
                    this.applyConfiguration(registration.GetLatestConfiguration());
                }
            }
        }

        private void Unregister(Registration registration)
        {
            lock (this.syncLock)
            {
                if (!registration.Registered)
                {
                    return;
                }

                var wasActive = ReferenceEquals(
                    this.registrations[this.registrations.Count - 1],
                    registration);
                registration.Registered = false;
                this.registrations.Remove(registration);

                if (!this.disposed && wasActive)
                {
                    var configuration = this.registrations.Count > 0
                        ? this.registrations[this.registrations.Count - 1].GetLatestConfiguration()
                        : SelfDiagnosticsConfiguration.Disabled;

                    this.applyConfiguration(configuration);
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private readonly SelfDiagnosticsConfigurationCoordinator owner;
            private readonly Microsoft.Extensions.Options.IOptionsMonitor<SelfDiagnosticsOptions> monitor;
            private readonly Lock configurationLock = new();

            private SelfDiagnosticsConfiguration latestConfiguration = SelfDiagnosticsConfiguration.Disabled;
#pragma warning disable CA2213 // Disposed through an atomic exchange in Dispose/DisposeSubscription.
            private IDisposable? subscription;
#pragma warning restore CA2213
            private int disposed;

            internal Registration(
                SelfDiagnosticsConfigurationCoordinator owner,
                Microsoft.Extensions.Options.IOptionsMonitor<SelfDiagnosticsOptions> monitor)
            {
                this.owner = owner;
                this.monitor = monitor;
            }

            internal bool Registered { get; set; }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref this.disposed, 1) != 0)
                {
                    return;
                }

                this.owner.Unregister(this);
                Interlocked.Exchange(ref this.subscription, null)?.Dispose();
            }

            internal void Subscribe()
            {
                this.Publish(this.monitor.CurrentValue);
                this.subscription = this.monitor.OnChange((options, _) => this.Publish(options));
                this.Publish(this.monitor.CurrentValue);
            }

            internal SelfDiagnosticsConfiguration GetLatestConfiguration()
            {
                lock (this.configurationLock)
                {
                    return this.latestConfiguration;
                }
            }

            internal void DisposeSubscription()
            {
                Interlocked.Exchange(ref this.subscription, null)?.Dispose();
            }

            private void Publish(SelfDiagnosticsOptions options)
            {
                var configuration = SelfDiagnosticsConfiguration.Create(options);

                lock (this.configurationLock)
                {
                    this.latestConfiguration = configuration;
                }

                this.owner.ConfigurationChanged(this);
            }
        }
    }
}

