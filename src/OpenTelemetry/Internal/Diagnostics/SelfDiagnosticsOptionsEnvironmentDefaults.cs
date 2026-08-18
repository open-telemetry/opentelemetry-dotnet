// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.SelfDiagnostics;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Internal;

internal static class SelfDiagnosticsOptionsEnvironmentDefaults
{
    private static readonly Action<string> DefaultConfigurationErrorReporter = static message =>
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch
        {
            // Nowhere left to report during options construction.
        }
    };

    /// <summary>
    /// Applies environment-variable defaults to <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The options object to update.</param>
    /// <param name="configuration">The configuration source to read from.</param>
    /// <param name="defaultLogDirectoryResolver">Resolves the default per-user log directory.</param>
    /// <param name="reportConfigurationError">Reports a configuration problem when the file sink cannot start.</param>
    /// <returns>The collected configuration warnings, or <see langword="null"/> when there were none.</returns>
    internal static List<string>? ApplyEnvironmentVariables(
        SelfDiagnosticsOptions options,
        IConfiguration configuration,
        Func<string?> defaultLogDirectoryResolver,
        Action<string>? reportConfigurationError = null)
    {
        List<string>? warnings = null;
        var reportError = reportConfigurationError ?? DefaultConfigurationErrorReporter;

        if (configuration.TryGetStringValue(SelfDiagnosticsOptions.LogLevelEnvVarName, out var logLevelRaw))
        {
            if (TryParseOtelLogLevel(logLevelRaw, out var parsedLevel))
            {
                options.MinimumLevel = parsedLevel;
            }
            else
            {
                AddWarning(ref warnings, SelfDiagnosticsOptions.LogLevelEnvVarName, logLevelRaw, "error, warn, info, debug, trace, none");
            }
        }

        if (configuration.TryGetStringValue(SelfDiagnosticsOptions.LogDirectoryEnvVarName, out var logDirectory))
        {
            options.LogDirectory = logDirectory;
        }

        if (configuration.TryGetStringValue(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, out var envVarModeRaw))
        {
            if (TryParseEnvironmentVariableLogMode(envVarModeRaw, out var parsedMode))
            {
                options.EnvironmentVariables = parsedMode;
            }
            else
            {
                AddWarning(ref warnings, SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, envVarModeRaw, "none, names, knownsafe, all");
            }
        }

        if (configuration.TryGetStringValue(SelfDiagnosticsOptions.SinksEnvVarName, out var sinksRaw))
        {
            var fileRequested = ApplySinks(options, sinksRaw, ref warnings);
            if (fileRequested)
            {
                EnsureFileLogDirectory(options, defaultLogDirectoryResolver, reportError, ref warnings);
            }
        }

        return warnings;
    }

    /// <summary>
    /// Parses an <c>OTEL_LOG_LEVEL</c> string value into a <see cref="LogLevel"/>.
    /// </summary>
    /// <remarks>
    /// The OpenTelemetry specification tokens are recognised first. The
    /// <see cref="LogLevel"/> member names are then accepted as an alias so that values
    /// such as <c>Warning</c> and <c>Information</c> are not silently ignored.
    /// </remarks>
    /// <param name="value">The raw <c>OTEL_LOG_LEVEL</c> string, for example <c>warn</c> or <c>debug</c>.</param>
    /// <param name="level">The parsed log level when the return value is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is recognised; otherwise <see langword="false"/>.</returns>
    internal static bool TryParseOtelLogLevel(string value, out LogLevel level)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "ERROR":
                level = LogLevel.Error;
                return true;

            case "WARNING":
            case "WARN":
                level = LogLevel.Warning;
                return true;

            case "INFO":
            case "INFORMATION":
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

        level = LogLevel.None;
        return false;
    }

    /// <summary>
    /// Parses an <c>OTEL_DOTNET_SELF_DIAGNOSTICS_ENV_VARS</c> value.
    /// </summary>
    /// <param name="value">The raw value, for example <c>knownsafe</c> or <c>all</c>.</param>
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
            case "KNOWNSAFE":
                mode = EnvironmentVariableLogMode.KnownSafeValues;
                return true;
            case "ALL":
                mode = EnvironmentVariableLogMode.AllValues;
                return true;
        }

        mode = EnvironmentVariableLogMode.KnownSafeValues;
        return false;
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
    /// other token.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> when the file sink was requested and not overridden by <c>none</c>.
    /// </returns>
    private static bool ApplySinks(SelfDiagnosticsOptions options, string value, ref List<string>? warnings)
    {
        var none = false;
        var fileRequested = false;

        foreach (var token in value.Split(','))
        {
            switch (token.Trim().ToUpperInvariant())
            {
                // Tolerate empty entries from trailing or doubled separators.
                case "":
                    break;

                case "NONE":
                    none = true;
                    break;

                case "FILE":
                    fileRequested = true;
                    break;

                case "STDOUT":
                    options.LogToStdout = true;
                    break;

                case "STDERR":
                    options.LogToStderr = true;
                    break;

                // Alias for 'stdout,stderr', for anyone arriving with the .NET
                // auto-instrumentation agent's vocabulary.
                case "CONSOLE":
                    options.LogToStdout = true;
                    options.LogToStderr = true;
                    break;

                default:
                    AddWarning(ref warnings, SelfDiagnosticsOptions.SinksEnvVarName, token.Trim(), SelfDiagnosticsOptions.SinksExpectedValues);
                    break;
            }
        }

        if (none)
        {
            options.LogToStdout = false;
            options.LogToStderr = false;
            options.LogDirectory = null;
            return false;
        }

        if (!fileRequested)
        {
            options.LogDirectory = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Ensures <see cref="SelfDiagnosticsOptions.LogDirectory"/> is set when the file sink was
    /// requested, falling back to <paramref name="defaultLogDirectoryResolver"/> when no
    /// explicit directory was supplied.
    /// </summary>
    private static void EnsureFileLogDirectory(
        SelfDiagnosticsOptions options,
        Func<string?> defaultLogDirectoryResolver,
        Action<string> reportConfigurationError,
        ref List<string>? warnings)
    {
        if (!string.IsNullOrEmpty(options.LogDirectory))
        {
            return;
        }

        options.LogDirectory = defaultLogDirectoryResolver();
        if (!string.IsNullOrEmpty(options.LogDirectory))
        {
            return;
        }

        var warning =
            $"{SelfDiagnosticsOptions.SinksEnvVarName} requested the 'file' sink but no default log directory could be resolved; no file will be written. Set {SelfDiagnosticsOptions.LogDirectoryEnvVarName} to a writable directory.";
        warnings ??= [];
        warnings.Add(warning);
        reportConfigurationError(warning);
    }
}
