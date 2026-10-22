// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
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
    /// Gets or sets messages describing configuration sources and values that could not be applied.
    /// These are surfaced in the log file preamble when a file sink is active. Environment
    /// variable configuration errors are also written to standard error when the file sink
    /// cannot be enabled (for example because no default log directory could be resolved).
    /// </summary>
    internal IReadOnlyList<string> ConfigurationWarnings { get; set; } = NoWarnings;

    /// <summary>
    /// Gets or sets a value indicating whether the file sink writes the path of each new log
    /// file to standard error. Disabled for the legacy <c>OTEL_DIAGNOSTICS.json</c> configuration,
    /// whose log directory is chosen explicitly and which never wrote to standard error.
    /// </summary>
    internal bool AnnounceLogFile { get; set; } = true;

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
            NoWarnings,
            true);

        private SelfDiagnosticsConfiguration(
            LogLevel minimumLevel,
            string? logDirectory,
            int fileSizeLimitKilobytes,
            int maxRetainedFiles,
            bool logToStdout,
            bool logToStderr,
            EnvironmentVariableLogMode environmentVariables,
            IReadOnlyList<string> configurationWarnings,
            bool announceLogFile)
        {
            this.MinimumLevel = minimumLevel;
            this.LogDirectory = logDirectory;
            this.FileSizeLimitKilobytes = fileSizeLimitKilobytes;
            this.MaxRetainedFiles = maxRetainedFiles;
            this.LogToStdout = logToStdout;
            this.LogToStderr = logToStderr;
            this.EnvironmentVariables = environmentVariables;
            this.ConfigurationWarnings = configurationWarnings;
            this.AnnounceLogFile = announceLogFile;
        }

        internal LogLevel MinimumLevel { get; }

        internal string? LogDirectory { get; }

        internal int FileSizeLimitKilobytes { get; }

        internal int MaxRetainedFiles { get; }

        internal bool LogToStdout { get; }

        internal bool LogToStderr { get; }

        internal EnvironmentVariableLogMode EnvironmentVariables { get; }

        internal IReadOnlyList<string> ConfigurationWarnings { get; }

        internal bool AnnounceLogFile { get; }

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
                options.ConfigurationWarnings,
                options.AnnounceLogFile);
        }
    }
}
