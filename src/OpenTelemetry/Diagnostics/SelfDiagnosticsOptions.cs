// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry;

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
///   <item><c>OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS</c> selects the sinks.</item>
///   <item><c>OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY</c> sets <see cref="LogDirectory"/>.</item>
///   <item><c>OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS</c> sets <see cref="EnvironmentVariables"/>.</item>
/// </list>
/// </para>
/// <para>
/// Only <c>OTEL_LOG_LEVEL</c> is shared with the wider ecosystem, and it activates nothing on its
/// own: with no sink selected the SDK stays silent whatever level is set. The variables that do
/// activate output are namespaced to this feature, so they cannot collide with the .NET
/// auto-instrumentation agent's own logging configuration and no agent detection is needed.
/// </para>
/// </remarks>
public sealed class SelfDiagnosticsOptions
{
    internal const string LogLevelEnvVarName = "OTEL_LOG_LEVEL";
    internal const string SinksEnvVarName = "OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS";
    internal const string LogDirectoryEnvVarName = "OTEL_DOTNET_SELF_DIAGNOSTICS_LOG_DIRECTORY";
    internal const string EnvironmentVariablesEnvVarName = "OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS";
    internal const string SinksExpectedValues = "none, file, stdout, stderr, console";

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
    /// <param name="configuration">The configuration source used to read OTEL_ environment variable defaults.</param>
    internal SelfDiagnosticsOptions(IConfiguration configuration)
    {
        Guard.ThrowIfNull(configuration);

        List<string>? warnings = null;

        if (configuration.TryGetStringValue(LogLevelEnvVarName, out var logLevelRaw))
        {
            if (TryParseOtelLogLevel(logLevelRaw, out var parsedLevel))
            {
                this.MinimumLevel = parsedLevel;
            }
            else
            {
                AddWarning(ref warnings, LogLevelEnvVarName, logLevelRaw, "error, warn, info, debug, trace, none");
            }
        }

        if (configuration.TryGetStringValue(LogDirectoryEnvVarName, out var logDir))
        {
            this.LogDirectory = logDir;
        }

        if (configuration.TryGetStringValue(EnvironmentVariablesEnvVarName, out var envVarModeRaw))
        {
            if (TryParseEnvironmentVariableLogMode(envVarModeRaw, out var parsedMode))
            {
                this.EnvironmentVariables = parsedMode;
            }
            else
            {
                AddWarning(ref warnings, EnvironmentVariablesEnvVarName, envVarModeRaw, "none, names, knownsafe, all");
            }
        }

        // Read after LogDirectory, because when present this variable is authoritative about
        // which sinks exist: a directory that was set without 'file' being listed is dropped.
        // When absent the sink set is inferred from LogDirectory alone, so setting only the
        // directory is still enough to get file output.
        if (configuration.TryGetStringValue(SinksEnvVarName, out var sinksRaw))
        {
            this.ApplySinks(sinksRaw, ref warnings);
        }

        this.ConfigurationWarnings = warnings is null ? NoWarnings : warnings.ToArray();
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
    /// Because this channel is intended to be safe to leave enabled in production, the more
    /// economical level is the better default; a default that the documentation would immediately
    /// tell readers to override is not a useful default.
    /// </para>
    /// <para>
    /// Set <c>OTEL_LOG_LEVEL=info</c>, or assign this property, to take the specification default.
    /// The level only takes effect once a sink is enabled - with no sink configured the SDK is
    /// silent at any level.
    /// </para>
    /// </remarks>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Gets or sets the directory in which self-diagnostics log files are written.
    /// Setting this property enables the file sink. <see langword="null"/> or empty
    /// disables file logging.
    /// </summary>
    public string? LogDirectory { get; set; }

    /// <summary>
    /// Gets or sets the maximum size of a single log file in kilobytes before it rolls
    /// over to a new file. Defaults to 10240 (10 MiB). A value less than or equal to zero
    /// disables size-based rollover. Files are never truncated; a new file is opened when
    /// the limit is reached. Positive values are not clamped to a minimum. A file may exceed
    /// the limit by its preamble and by the entry that crosses the boundary, so very small
    /// values can cause a rollover after nearly every entry.
    /// </summary>
    public int FileSizeLimitKilobytes { get; set; } = 10_240;

    /// <summary>
    /// Gets or sets the maximum number of rolling log files to retain.
    /// When a new file is opened the oldest is deleted if this limit would be exceeded.
    /// Values less than or equal to zero disable automatic pruning, so files are retained
    /// until they are removed externally. Defaults to zero.
    /// </summary>
    public int MaxRetainedFiles { get; set; }

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
    /// Gets or sets how much of the <c>OTEL_*</c> environment variable snapshot is written to
    /// the log file preamble. Defaults to <see cref="EnvironmentVariableLogMode.KnownSafeValues"/>,
    /// which lists every variable name but redacts the value of any variable the SDK does not
    /// recognise as safe to disclose.
    /// </summary>
    public EnvironmentVariableLogMode EnvironmentVariables { get; set; }
        = EnvironmentVariableLogMode.KnownSafeValues;

    /// <summary>
    /// Gets messages describing environment variable values that could not be applied.
    /// These are surfaced in the log file preamble because no logger exists at the point
    /// options are constructed.
    /// </summary>
    internal IReadOnlyList<string> ConfigurationWarnings { get; private set; } = NoWarnings;

    /// <summary>
    /// Parses an OTEL_LOG_LEVEL string value into a <see cref="LogLevel"/>.
    /// </summary>
    /// <remarks>
    /// The OpenTelemetry specification tokens are recognised first. The
    /// <see cref="LogLevel"/> member names are then accepted as an alias so that values a .NET
    /// developer would reasonably write - <c>Warning</c>, <c>Information</c>, <c>Critical</c> -
    /// are not silently ignored. Numeric input is rejected: <c>OTEL_LOG_LEVEL=0</c> resolving to
    /// <see cref="LogLevel.Trace"/> would be a trap rather than a convenience.
    /// </remarks>
    /// <param name="value">The raw OTEL_LOG_LEVEL string (e.g. "warn", "debug").</param>
    /// <param name="level">The parsed <see cref="LogLevel"/> when the return value is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is a recognized level string; otherwise <see langword="false"/>.</returns>
    internal static bool TryParseOtelLogLevel(string value, out LogLevel level)
    {
        switch (value.Trim().ToUpperInvariant())
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
        }

        if (!StartsWithNumericSign(value)
            && Enum.TryParse(value.Trim(), ignoreCase: true, out level))
        {
            return true;
        }

        level = LogLevel.None;
        return false;
    }

    /// <summary>
    /// Parses an <c>OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS</c> value.
    /// </summary>
    /// <param name="value">The raw value (e.g. "known", "all").</param>
    /// <param name="mode">The parsed mode when the return value is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is recognised; otherwise <see langword="false"/>.</returns>
    internal static bool TryParseEnvironmentVariableLogMode(string value, out EnvironmentVariableLogMode mode)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "NONE":
                mode = EnvironmentVariableLogMode.None;
                return true;
            case "NAMES":
                mode = EnvironmentVariableLogMode.Names;
                return true;

            // Both the short token and the enum member name are accepted, so a value copied
            // from code works in the environment variable and vice versa.
            case "KNOWN":
            case "KNOWNSAFE":
            case "KNOWNSAFEVALUES":
                mode = EnvironmentVariableLogMode.KnownSafeValues;
                return true;
            case "ALL":
            case "ALLVALUES":
                mode = EnvironmentVariableLogMode.AllValues;
                return true;
        }

        mode = EnvironmentVariableLogMode.KnownSafeValues;
        return false;
    }

    private static bool StartsWithNumericSign(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var first = trimmed[0];
        return char.IsDigit(first) || first == '+' || first == '-';
    }

    private static void AddWarning(ref List<string>? warnings, string name, string value, string expected)
    {
        warnings ??= [];
        warnings.Add($"{name}='{value}' is not a recognised value and was ignored. Expected one of: {expected}.");
    }

    /// <summary>
    /// Applies a comma-separated sink selection, e.g. <c>file,stderr</c>.
    /// </summary>
    /// <remarks>
    /// Unrecognised tokens are reported and skipped rather than invalidating the whole value, so
    /// one typo does not silence the sinks that were spelled correctly. <c>none</c> overrides every
    /// other token: silence is the safe reading of a contradictory value.
    /// </remarks>
    private void ApplySinks(string value, ref List<string>? warnings)
    {
        var silence = false;
        var fileRequested = false;

        foreach (var token in value.Split(','))
        {
            switch (token.Trim().ToUpperInvariant())
            {
                case "":
                    // Tolerate empty entries from trailing or doubled separators.
                    break;

                case "NONE":
                    silence = true;
                    break;

                case "FILE":
                    fileRequested = true;
                    break;

                case "STDOUT":
                    this.LogToStdout = true;
                    break;

                case "STDERR":
                    this.LogToStderr = true;
                    break;

                case "CONSOLE":
                    // Alias for 'stdout,stderr', for anyone arriving with the .NET
                    // auto-instrumentation agent's vocabulary.
                    this.LogToStdout = true;
                    this.LogToStderr = true;
                    break;

                default:
                    AddWarning(ref warnings, SinksEnvVarName, token.Trim(), SinksExpectedValues);
                    break;
            }
        }

        if (silence)
        {
            this.MinimumLevel = LogLevel.None;
            this.LogToStdout = false;
            this.LogToStderr = false;
            this.LogDirectory = null;
            return;
        }

        if (!fileRequested)
        {
            // The selection is authoritative: a directory not accompanied by 'file' is not a
            // request for file output.
            this.LogDirectory = null;
        }
        else if (string.IsNullOrEmpty(this.LogDirectory))
        {
            warnings ??= [];
            warnings.Add(
                $"{SinksEnvVarName} requested the 'file' sink but {LogDirectoryEnvVarName} is not set; no file will be written.");
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

        // Aliases an entry in `registrations`; the lease returned from Register owns its lifetime.
#pragma warning disable CA2213 // Not owned here - disposing it would revoke the caller's lease.
        private Registration? active;
#pragma warning restore CA2213

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
                this.active = null;

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
            var selected = this.SelectActiveUnderLock();
            var ownerChanged = !ReferenceEquals(selected, this.active);
            this.active = selected;

            if (!ownerChanged && (changed is null || !ReferenceEquals(changed, selected)))
            {
                // The owner is unchanged and the change came from a registration that does not
                // own the configuration. Re-applying an identical configuration would churn the
                // sink set for no reason.
                return;
            }

            this.applyConfiguration(
                selected?.GetLatestConfiguration() ?? SelfDiagnosticsConfiguration.Disabled);
        }

        private Registration? SelectActiveUnderLock()
        {
            for (var i = this.registrations.Count - 1; i >= 0; i--)
            {
                if (this.registrations[i].GetLatestConfiguration().HasConfiguredSink)
                {
                    return this.registrations[i];
                }
            }

            return this.registrations.Count > 0
                ? this.registrations[this.registrations.Count - 1]
                : null;
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
