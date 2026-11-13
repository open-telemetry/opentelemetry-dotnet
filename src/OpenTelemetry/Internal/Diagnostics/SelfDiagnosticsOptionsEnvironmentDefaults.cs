// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.SelfDiagnostics;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Internal;

internal static class SelfDiagnosticsOptionsEnvironmentDefaults
{
    // Section keys set by the same option as a variable. When ApplyEnvironmentVariables applied
    // the variable, the key from the later provider wins.
    internal static readonly ConfigurationAlias[] SectionKeyAliases =
    [
        new(
            nameof(SelfDiagnosticsOptions.MinimumLevel),
            SelfDiagnosticsOptions.LogLevelEnvVarName,
            static value => TryParseOtelLogLevel(value, out var level) ? level.ToString() : value),
        new(nameof(SelfDiagnosticsOptions.LogToStdout), SelfDiagnosticsOptions.SinksEnvVarName),
        new(nameof(SelfDiagnosticsOptions.LogToStderr), SelfDiagnosticsOptions.SinksEnvVarName),
        new(
            nameof(SelfDiagnosticsOptions.EnvironmentVariables),
            SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName,
            static value => TryParseEnvironmentVariableLogMode(value, out var mode) ? mode.ToString() : value),
    ];

    // LogDirectory depends on two variables, the directory and the sinks selection, so
    // ApplyEnvironmentVariables resolves it against the section itself and binding must not overwrite it.
    internal static readonly string[] SectionKeysResolvedWithVariables = [nameof(SelfDiagnosticsOptions.LogDirectory)];

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
    /// <param name="section">The <c>OpenTelemetry:SelfDiagnostics</c> section. <see langword="null"/> for named options.</param>
    /// <param name="appliedVariables">Receives the names of the variables whose values were applied. <see langword="null"/> when not needed.</param>
    /// <returns>The collected configuration warnings, or <see langword="null"/> when there were none.</returns>
    internal static List<string>? ApplyEnvironmentVariables(
        SelfDiagnosticsOptions options,
        IConfiguration configuration,
        Func<string?> defaultLogDirectoryResolver,
        Action<string>? reportConfigurationError = null,
        IConfiguration? section = null,
        ISet<string>? appliedVariables = null)
    {
        List<string>? warnings = null;
        var reportError = reportConfigurationError ?? DefaultConfigurationErrorReporter;

        if (configuration.TryGetStringValue(SelfDiagnosticsOptions.LogLevelEnvVarName, out var logLevelRaw))
        {
            if (TryParseOtelLogLevel(logLevelRaw, out var parsedLevel))
            {
                options.MinimumLevel = parsedLevel;
                appliedVariables?.Add(SelfDiagnosticsOptions.LogLevelEnvVarName);
            }
            else
            {
                AddWarning(ref warnings, SelfDiagnosticsOptions.LogLevelEnvVarName, logLevelRaw, SelfDiagnosticsOptions.LogLevelExpectedValues);
            }
        }

        if (configuration.TryGetStringValue(SelfDiagnosticsOptions.LogDirectoryEnvVarName, out var logDirectory))
        {
            options.LogDirectory = logDirectory;
            appliedVariables?.Add(SelfDiagnosticsOptions.LogDirectoryEnvVarName);
        }

        // Resolve directory precedence before sink selection: later providers win, with ties going to the section.
        // Keep explicit empty section values, which disable file output rather than request a default directory.
        var sectionDirectory = section?[nameof(SelfDiagnosticsOptions.LogDirectory)];
        var directoryKey = ConfigurationPath.Combine(SelfDiagnosticsOptions.ConfigurationSectionPath, nameof(SelfDiagnosticsOptions.LogDirectory));

        var sectionDirectoryWins = sectionDirectory != null
            && (string.IsNullOrWhiteSpace(logDirectory)
                || !ConfigurationAliasPrecedence.IsFromLaterProvider(configuration, SelfDiagnosticsOptions.LogDirectoryEnvVarName, directoryKey));

        if (sectionDirectoryWins)
        {
            options.LogDirectory = sectionDirectory;
        }

        if (configuration.TryGetStringValue(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, out var envVarModeRaw))
        {
            if (TryParseEnvironmentVariableLogMode(envVarModeRaw, out var parsedMode))
            {
                options.EnvironmentVariables = parsedMode;
                appliedVariables?.Add(SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName);
            }
            else
            {
                AddWarning(ref warnings, SelfDiagnosticsOptions.EnvironmentVariablesEnvVarName, envVarModeRaw, "none, names, knownsafe, all");
            }
        }

        if (configuration.TryGetStringValue(SelfDiagnosticsOptions.SinksEnvVarName, out var sinksRaw)
            && TryApplySinks(options, sinksRaw, ref warnings, out var fileRequested))
        {
            appliedVariables?.Add(SelfDiagnosticsOptions.SinksEnvVarName);

            // Sink selection can clear the directory, so restore a section value unless the sinks came from a later provider.
            if (sectionDirectoryWins
                && !ConfigurationAliasPrecedence.IsFromLaterProvider(configuration, SelfDiagnosticsOptions.SinksEnvVarName, directoryKey))
            {
                options.LogDirectory = sectionDirectory;
            }
            else if (fileRequested)
            {
                // Only resolve a fallback after precedence and sink selection have been settled.
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

            default:
                level = LogLevel.None;
                return false;
        }
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

            default:
                mode = EnvironmentVariableLogMode.KnownSafeValues;
                return false;
        }
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
    /// one typo does not silence the sinks that were spelled correctly. A selection with no
    /// recognised token is ignored, like any other unrecognised value. <c>none</c> overrides every
    /// other token.
    /// </remarks>
    /// <param name="options">The options object to update.</param>
    /// <param name="value">The raw <c>OTEL_DOTNET_SELF_DIAGNOSTICS_SINKS</c> value.</param>
    /// <param name="warnings">The collected configuration warnings.</param>
    /// <param name="fileRequested">
    /// <see langword="true"/> when the file sink was requested and not overridden by <c>none</c>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the selection names a recognised sink and was applied; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryApplySinks(SelfDiagnosticsOptions options, string value, ref List<string>? warnings, out bool fileRequested)
    {
        fileRequested = false;

        var none = false;
        var file = false;
        var stdout = false;
        var stderr = false;

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
                    file = true;
                    break;

                case "STDOUT":
                    stdout = true;
                    break;

                case "STDERR":
                    stderr = true;
                    break;

                // Alias for 'stdout,stderr', for anyone arriving with the .NET
                // auto-instrumentation agent's vocabulary.
                case "CONSOLE":
                    stdout = true;
                    stderr = true;
                    break;

                default:
                    AddWarning(ref warnings, SelfDiagnosticsOptions.SinksEnvVarName, token.Trim(), SelfDiagnosticsOptions.SinksExpectedValues);
                    break;
            }
        }

        if (!none && !file && !stdout && !stderr)
        {
            return false;
        }

        if (none)
        {
            options.LogToStdout = false;
            options.LogToStderr = false;
            options.LogDirectory = null;
            return true;
        }

        options.LogToStdout |= stdout;
        options.LogToStderr |= stderr;

        if (!file)
        {
            options.LogDirectory = null;
        }

        fileRequested = file;
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
