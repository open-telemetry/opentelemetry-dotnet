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
/// Self-diagnostics is off by default. To enable it, set <see cref="LogDirectory"/>,
/// <see cref="LogToStdout"/>, or <see cref="LogToStderr"/>. Setting <see cref="MinimumLevel"/>
/// alone produces no output.
/// </para>
/// <para>
/// Options can also be set from <c>OTEL_*</c> environment variables or the
/// <c>OpenTelemetry:SelfDiagnostics</c> configuration section. Values set in code take
/// precedence, and changes made through <c>IOptionsMonitor</c> apply without rebuilding the
/// provider. See the troubleshooting guide for the full list of variables and precedence rules.
/// </para>
/// </remarks>
public sealed class SelfDiagnosticsOptions
{
    internal const string ConfigurationSectionPath = "OpenTelemetry:SelfDiagnostics";
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

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfDiagnosticsOptions"/> class,
    /// applying environment-variable defaults from <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The configuration to read <c>OTEL_*</c> environment variables from.</param>
    /// <param name="defaultLogDirectoryResolver">Resolves the folder used when the file sink is requested without a directory.</param>
    /// <param name="reportConfigurationError">Called when the file sink cannot be enabled; defaults to writing to standard error.</param>
    /// <param name="section">The <c>OpenTelemetry:SelfDiagnostics</c> section, so its <c>LogDirectory</c> value can take precedence over the environment variables; <see langword="null"/> for named options.</param>
    /// <param name="appliedVariables">Receives the names of the environment variables whose values were applied; <see langword="null"/> when not needed.</param>
    internal SelfDiagnosticsOptions(
        IConfiguration configuration,
        Func<string?> defaultLogDirectoryResolver,
        Action<string>? reportConfigurationError = null,
        IConfiguration? section = null,
        ISet<string>? appliedVariables = null)
    {
        Guard.ThrowIfNull(configuration);
        Guard.ThrowIfNull(defaultLogDirectoryResolver);

        var warnings = SelfDiagnosticsOptionsEnvironmentDefaults.ApplyEnvironmentVariables(
            this,
            configuration,
            defaultLogDirectoryResolver,
            reportConfigurationError,
            section,
            appliedVariables);

        this.ConfigurationWarnings = warnings is null ? NoWarnings : [.. warnings];
    }

    /// <summary>
    /// Gets or sets the minimum log level. Events below this level are discarded, and
    /// <see cref="LogLevel.None"/> disables logging. Defaults to <see cref="LogLevel.Warning"/>.
    /// </summary>
    /// <remarks>
    /// This differs from the OpenTelemetry specification default of <c>info</c>; set
    /// <c>OTEL_LOG_LEVEL=info</c> to match it.
    /// </remarks>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Gets or sets the folder where self-diagnostics log files are written. Setting a non-empty
    /// value enables file logging. Defaults to <see langword="null"/> (no file logging).
    /// </summary>
    /// <remarks>
    /// Use <see cref="GetDefaultLogDirectory"/> to log to the standard per-user location.
    /// </remarks>
    public string? LogDirectory { get; set; }

    /// <summary>
    /// Gets or sets the size in kilobytes at which the current log file is closed and a new one
    /// is started. Defaults to 10240 (10 MiB). A value less than or equal to zero disables
    /// size-based rollover.
    /// </summary>
    public int FileSizeLimitKilobytes { get; set; } = DefaultFileSizeLimitKilobytes;

    /// <summary>
    /// Gets or sets the maximum number of log files to keep. When a new file is started, the
    /// oldest files are deleted to stay within this limit. Defaults to 10. A value less than
    /// or equal to zero keeps all files.
    /// </summary>
    public int MaxRetainedFiles { get; set; } = DefaultMaxRetainedFiles;

    /// <summary>
    /// Gets or sets a value indicating whether SDK diagnostics are written to standard output.
    /// </summary>
    /// <remarks>
    /// When both this and <see cref="LogToStderr"/> are set, <see cref="LogLevel.Error"/> and
    /// <see cref="LogLevel.Critical"/> entries go to standard error and all other entries go to
    /// standard output.
    /// </remarks>
    public bool LogToStdout { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether SDK diagnostics are written to standard error.
    /// </summary>
    /// <remarks>
    /// When both this and <see cref="LogToStdout"/> are set, <see cref="LogLevel.Error"/> and
    /// <see cref="LogLevel.Critical"/> entries go to standard error and all other entries go to
    /// standard output.
    /// </remarks>
    public bool LogToStderr { get; set; }

    /// <summary>
    /// Gets or sets how much of the <c>OTEL_*</c> environment variable snapshot is written to
    /// the log file preamble. Defaults to <see cref="EnvironmentVariableLogMode.KnownSafeValues"/>,
    /// which lists every variable name but redacts the value of any variable the SDK does not
    /// recognise as safe to disclose. Only applies when file logging is enabled.
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
    /// Gets the standard per-user folder for self-diagnostics log files on the current platform.
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
    /// This is the folder used when <c>OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS</c> includes
    /// <c>file</c> without a directory. Assigning a <see langword="null"/> result to
    /// <see cref="LogDirectory"/> leaves file logging disabled.
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
