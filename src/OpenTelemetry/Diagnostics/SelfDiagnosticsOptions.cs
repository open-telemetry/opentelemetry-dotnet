// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.SelfDiagnostics;

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
/// callbacks, which always take precedence:
/// <list type="bullet">
///   <item><c>OTEL_LOG_LEVEL</c> sets <see cref="MinimumLevel"/>.</item>
///   <item><c>OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS</c> selects the sinks. When the list includes
///   <c>file</c> and <c>OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY</c> is unset, the SDK tries
///   <see cref="GetDefaultLogDirectory"/>.</item>
///   <item><c>OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY</c> sets <see cref="LogDirectory"/> and
///   takes precedence over the platform default.</item>
///   <item><c>OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS</c> sets <see cref="EnvironmentVariables"/>.</item>
/// </list>
/// </para>
/// <para>
/// Only <c>OTEL_LOG_LEVEL</c> is shared with the wider ecosystem, and it activates nothing on its
/// own: with no sink selected the SDK stays silent whatever level is set.
/// </para>
/// </remarks>
public sealed class SelfDiagnosticsOptions
{
    internal const string LogLevelEnvVarName = "OTEL_LOG_LEVEL";
    internal const string SinksEnvVarName = "OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS";
    internal const string LogDirectoryEnvVarName = "OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY";
    internal const string EnvironmentVariablesEnvVarName = "OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS";
    internal const string LogLevelExpectedValues = "error, warn, info, debug, trace, none";
    internal const string SinksExpectedValues = "none, file, stdout, stderr, console";
    internal const int DefaultFileSizeLimitKilobytes = 10_240;
    internal const int DefaultMaxRetainedFiles = 10;

    private static readonly string[] NoWarnings = [];

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
    /// <param name="configuration">The configuration source used to read <c>OTEL_</c> environment variable defaults.</param>
    internal SelfDiagnosticsOptions(IConfiguration configuration)
        : this(configuration, GetDefaultLogDirectory)
    {
    }

    internal SelfDiagnosticsOptions(
        IConfiguration configuration,
        Func<string?> defaultLogDirectoryResolver,
        Action<string>? reportConfigurationError = null)
    {
        Guard.ThrowIfNull(configuration);
        Guard.ThrowIfNull(defaultLogDirectoryResolver);

        var warnings = SelfDiagnosticsOptionsEnvironmentDefaults.ApplyEnvironmentVariables(
            this,
            configuration,
            defaultLogDirectoryResolver,
            reportConfigurationError);

        this.ConfigurationWarnings = warnings is null ? NoWarnings : [.. warnings];
    }

    /// <summary>
    /// Gets or sets the minimum log level. Events below this level are discarded.
    /// Defaults to <see cref="LogLevel.Warning"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This deliberately diverges from the OpenTelemetry specification, whose default for
    /// <c>OTEL_LOG_LEVEL</c> is <c>info</c>. <see cref="LogLevel.Information"/> and below are
    /// considerably more verbose, and that verbosity costs log volume, disk, and sink throughput
    /// without usually adding diagnostic value until something is actually being investigated.
    /// </para>
    /// <para>
    /// Set <c>OTEL_LOG_LEVEL=info</c>, or assign this property, to take the specification default.
    /// </para>
    /// </remarks>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Gets or sets the folder where self-diagnostics log files are written.
    /// Set this to use a specific folder. Leave it <see langword="null"/> or empty to leave
    /// file logging unset; if <c>OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS</c> requests <c>file</c>,
    /// the SDK may still choose a default folder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS</c> includes <c>file</c> and
    /// <c>OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY</c> is unset, the environment-variable
    /// constructor tries to fill this property from <see cref="GetDefaultLogDirectory"/>.
    /// From code, assign <see cref="GetDefaultLogDirectory"/> (or an explicit path) yourself;
    /// a blank <see cref="SelfDiagnosticsOptions"/> instance does not pick a default folder
    /// on its own.
    /// </para>
    /// </remarks>
    public string? LogDirectory { get; set; }

    /// <summary>
    /// Gets or sets the maximum size of a single log file in kilobytes before it rolls
    /// over to a new file. Defaults to 10240 (10 MiB). A value less than or equal to zero
    /// disables size-based rollover. Files are never truncated; a new file is opened when
    /// the limit is reached. Positive values are not clamped to a minimum. A file may exceed
    /// the limit by its preamble and by the entry that crosses the boundary, so very small
    /// values can cause a rollover after nearly every entry.
    /// </summary>
    public int FileSizeLimitKilobytes { get; set; } = DefaultFileSizeLimitKilobytes;

    /// <summary>
    /// Gets or sets the maximum number of rolling log files to retain. Defaults to 10.
    /// When a new file is opened the oldest is deleted if this limit would be exceeded.
    /// Values less than or equal to zero disable automatic pruning, so files are retained
    /// until they are removed externally.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default retains a bounded history so long-running deployments and repeated
    /// reproductions cannot fill the disk. Set a value less than or equal to zero to retain
    /// every rolled file indefinitely instead, or a different positive value to change the
    /// number retained.
    /// </para>
    /// </remarks>
    public int MaxRetainedFiles { get; set; } = DefaultMaxRetainedFiles;

    /// <summary>
    /// Gets or sets a value indicating whether SDK diagnostics are written to standard output.
    /// When only this flag is set all entries are written to standard output. When both this flag and
    /// <see cref="LogToStderr"/> are set, entries at <see cref="LogLevel.Warning"/> and below are written to
    /// standard output and entries above <see cref="LogLevel.Warning"/> are written to standard error.
    /// </summary>
    public bool LogToStdout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether SDK diagnostics are written to standard error.
    /// When only this flag is set all entries are written to standard error. When both this flag and
    /// <see cref="LogToStdout"/> are set, entries above <see cref="LogLevel.Warning"/> are written to standard
    /// error and entries at <see cref="LogLevel.Warning"/> and below are written to standard output.
    /// </summary>
    public bool LogToStderr { get; set; }

    /// <summary>
    /// Gets or sets how much of the <c>OTEL_*</c> environment variable snapshot is written to
    /// the log file preamble. Defaults to <see cref="EnvironmentVariableLogMode.KnownSafeValues"/>,
    /// which lists every variable name but redacts the value of any variable the SDK does not
    /// recognise as safe to disclose.
    /// </summary>
    public EnvironmentVariableLogMode EnvironmentVariables { get; set; }
        = EnvironmentVariableLogMode.KnownSafeValues;

    /// <summary>
    /// Gets messages describing environment variable values that could not be applied.
    /// These are surfaced in the log file preamble when a file sink is active. When the file
    /// sink cannot be enabled (for example because no default log directory could be resolved),
    /// the same messages are also written to standard error so the failure is not silent.
    /// </summary>
    internal IReadOnlyList<string> ConfigurationWarnings { get; private set; } = NoWarnings;

    /// <summary>
    /// Resolves the platform-appropriate per-user directory used when the file sink is enabled
    /// without an explicit <see cref="LogDirectory"/>.
    /// </summary>
    /// <returns>
    /// The default log directory for the current platform, or <see langword="null"/> when no
    /// per-user folder can be resolved.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Typical paths:
    /// <list type="bullet">
    ///   <item>Windows: <c>%LOCALAPPDATA%\OpenTelemetry\dotnet-diagnostics</c></item>
    ///   <item>macOS: <c>~/Library/Logs/OpenTelemetry/dotnet-diagnostics</c></item>
    ///   <item>Linux and other Unix-like systems: <c>$XDG_STATE_HOME/opentelemetry/dotnet-diagnostics</c>
    ///   when <c>XDG_STATE_HOME</c> is an absolute path; otherwise
    ///   <c>~/.local/state/opentelemetry/dotnet-diagnostics</c></item>
    /// </list>
    /// </para>
    /// <para>
    /// Prefer this helper from <c>Configure&lt;SelfDiagnosticsOptions&gt;</c> callbacks so code
    /// configuration matches the environment-variable default used when
    /// <c>OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS</c> includes <c>file</c> without a directory.
    /// If no per-user folder can be found, file logging stays off and the SDK writes a warning
    /// to standard error.
    /// </para>
    /// </remarks>
    public static string? GetDefaultLogDirectory()
        => SelfDiagnosticsLogDirectoryResolver.Resolve();

    /// <summary>
    /// Immutable, internally-consistent snapshot of self-diagnostics options.
    /// </summary>
    internal sealed class SelfDiagnosticsConfiguration
    {
        internal static readonly SelfDiagnosticsConfiguration Disabled = new(
            LogLevel.None,
            null,
            DefaultFileSizeLimitKilobytes,
            DefaultMaxRetainedFiles,
            false,
            false,
            EnvironmentVariableLogMode.KnownSafeValues,
            NoWarnings);

        private SelfDiagnosticsConfiguration(
            LogLevel minimumLevel,
            string? logDirectory,
            int fileSizeLimitKilobytes,
            int maxRetainedFiles,
            bool logToStdout,
            bool logToStderr,
            EnvironmentVariableLogMode environmentVariables,
            IReadOnlyList<string> configurationWarnings)
        {
            this.MinimumLevel = minimumLevel;
            this.LogDirectory = logDirectory;
            this.FileSizeLimitKilobytes = fileSizeLimitKilobytes;
            this.MaxRetainedFiles = maxRetainedFiles;
            this.LogToStdout = logToStdout;
            this.LogToStderr = logToStderr;
            this.EnvironmentVariables = environmentVariables;
            this.ConfigurationWarnings = configurationWarnings;
        }

        internal LogLevel MinimumLevel { get; }

        internal string? LogDirectory { get; }

        internal int FileSizeLimitKilobytes { get; }

        internal int MaxRetainedFiles { get; }

        internal bool LogToStdout { get; }

        internal bool LogToStderr { get; }

        internal EnvironmentVariableLogMode EnvironmentVariables { get; }

        internal IReadOnlyList<string> ConfigurationWarnings { get; }

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
                options.LogToStderr,
                options.EnvironmentVariables,
                options.ConfigurationWarnings);
        }
    }

    /// <summary>
    /// Coordinates process-global configuration supplied by independently-owned providers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ownership goes to the most recently registered provider that actually configured a sink,
    /// falling back to the most recent registration when none has. Selecting on configured-sink
    /// rather than purely on recency matters because every provider build registers: without it,
    /// an unrelated <c>Sdk.CreateTracerProviderBuilder().Build()</c> in a library, a health
    /// check, or a parallel test would take ownership with default (silent) options and
    /// silently switch off the diagnostics the application had asked for.
    /// </para>
    /// </remarks>
    internal sealed class SelfDiagnosticsConfigurationCoordinator : IDisposable
    {
        private readonly Lock syncLock = new();
        private readonly List<Registration> registrations = [];
        private readonly Action<SelfDiagnosticsConfiguration> applyConfiguration;

        private object? activeRegistrationIdentity;
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
                this.activeRegistrationIdentity = null;

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

        internal IDisposable Register(IOptionsMonitor<SelfDiagnosticsOptions> monitor)
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
                this.ReevaluateActiveUnderLock(changed: registration);
            }

            return registration;
        }

        /// <summary>
        /// Recomputes which registration owns the process-global configuration and applies its
        /// configuration when either the owner changed or the current owner's own configuration
        /// changed. Must be called while holding <see cref="syncLock"/>.
        /// </summary>
        /// <param name="changed">The registration whose configuration just changed, if any.</param>
        private void ReevaluateActiveUnderLock(Registration? changed)
        {
            var selectedIndex = this.SelectActiveUnderLock();
            var selectedRegistration = selectedIndex >= 0 ? this.registrations[selectedIndex] : null;
            var selectedIdentity = selectedRegistration?.Identity;
            var ownerChanged = !ReferenceEquals(selectedIdentity, this.activeRegistrationIdentity);
            this.activeRegistrationIdentity = selectedIdentity;

            if (!ownerChanged && (changed is null || !ReferenceEquals(changed, selectedRegistration)))
            {
                // The owner is unchanged and the change came from a registration that does not
                // own the configuration. Re-applying an identical configuration would churn the
                // sink set for no reason.
                return;
            }

            this.applyConfiguration(
                selectedRegistration?.GetLatestConfiguration() ?? SelfDiagnosticsConfiguration.Disabled);
        }

        private int SelectActiveUnderLock()
        {
            for (var i = this.registrations.Count - 1; i >= 0; i--)
            {
                if (this.registrations[i].GetLatestConfiguration().HasConfiguredSink)
                {
                    return i;
                }
            }

            return this.registrations.Count > 0
                ? this.registrations.Count - 1
                : -1;
        }

        private void ConfigurationChanged(Registration registration)
        {
            lock (this.syncLock)
            {
                if (!this.disposed && registration.Registered)
                {
                    this.ReevaluateActiveUnderLock(changed: registration);
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

                registration.Registered = false;
                this.registrations.Remove(registration);

                if (!this.disposed)
                {
                    // The registration is already out of the list, so if it was the owner the
                    // re-selection below cannot pick it again and the owner-changed path applies.
                    this.ReevaluateActiveUnderLock(changed: null);
                }
            }
        }

        private sealed class Registration : IDisposable
        {
            private readonly SelfDiagnosticsConfigurationCoordinator owner;
            private readonly IOptionsMonitor<SelfDiagnosticsOptions> monitor;
            private readonly Lock configurationLock = new();

            private SelfDiagnosticsConfiguration latestConfiguration = SelfDiagnosticsConfiguration.Disabled;
#pragma warning disable CA2213 // Disposed through an atomic exchange in Dispose/DisposeSubscription.
            private IDisposable? subscription;
#pragma warning restore CA2213
            private int disposed;

            internal Registration(
                SelfDiagnosticsConfigurationCoordinator owner,
                IOptionsMonitor<SelfDiagnosticsOptions> monitor)
            {
                this.owner = owner;
                this.monitor = monitor;
            }

            // Stable sentinel token used by the coordinator to track the active owner via
            // ReferenceEquals. Separate from the Registration reference itself so the
            // coordinator field can be typed as object? without a circular dependency.
            internal object Identity { get; } = new();

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

            internal void DisposeSubscription() =>
                Interlocked.Exchange(ref this.subscription, null)?.Dispose();

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
