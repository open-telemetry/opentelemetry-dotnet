// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Internal;

/// <summary>
/// Parses the self-diagnostics configuration file.
/// </summary>
internal sealed partial class SelfDiagnosticsConfigParser
{
    public const string ConfigFileName = "OTEL_DIAGNOSTICS.json";

    /// <summary>
    /// ConfigBufferSize is the maximum bytes of config file that will be read.
    /// </summary>
    private const int ConfigBufferSize = 4 * 1024;

    private const string LogDirectoryRegexPattern = @"""LogDirectory""\s*:\s*""(?<LogDirectory>.*?)""";
    private const string FileSizeRegexPattern = @"""FileSize""\s*:\s*(?<FileSize>\d+)";
    private const string LogLevelRegexPattern = @"""LogLevel""\s*:\s*""(?<LogLevel>.*?)""";

#if !NET
    private static readonly Regex LogDirectoryRegex = new(LogDirectoryRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FileSizeRegex = new(FileSizeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LogLevelRegex = new(LogLevelRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
#endif

    private readonly Func<string?> configFileLocator;

    public SelfDiagnosticsConfigParser()
        : this(() => LocateConfigFile(Environment.CurrentDirectory, AppContext.BaseDirectory))
    {
    }

    internal SelfDiagnosticsConfigParser(Func<string?> configFileLocator)
    {
        Guard.ThrowIfNull(configFileLocator);
        this.configFileLocator = configFileLocator;
    }

    /// <summary>
    /// Returns the path of the config file in <paramref name="workingDirectory"/>, falling back to
    /// <paramref name="baseDirectory"/>, or <see langword="null"/> when neither has one.
    /// </summary>
    /// <param name="workingDirectory">The directory searched first.</param>
    /// <param name="baseDirectory">The directory searched when the working directory has no file.</param>
    /// <returns>The config file path, or <see langword="null"/>.</returns>
    internal static string? LocateConfigFile(string workingDirectory, string baseDirectory)
    {
        var path = Path.Combine(workingDirectory, ConfigFileName);
        if (File.Exists(path))
        {
            return path;
        }

        path = Path.Combine(baseDirectory, ConfigFileName);
        return File.Exists(path) ? path : null;
    }

    internal static bool TryParseLogDirectory(
        string configJson,
        out string logDirectory)
    {
        var logDirectoryResult = GetLogDirectoryRegex().Match(configJson);
        logDirectory = logDirectoryResult.Groups["LogDirectory"].Value;
        return logDirectoryResult.Success && !string.IsNullOrWhiteSpace(logDirectory);
    }

    internal static bool TryParseFileSize(string configJson, out int fileSizeInKB)
    {
        fileSizeInKB = 0;
        var fileSizeResult = GetFileSizeRegex().Match(configJson);
        return fileSizeResult.Success &&
               int.TryParse(fileSizeResult.Groups["FileSize"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out fileSizeInKB);
    }

    internal static bool TryParseLogLevel(
        string configJson,
        [NotNullWhen(true)]
        out string? logLevel)
    {
        var logLevelResult = GetLogLevelRegex().Match(configJson);
        logLevel = logLevelResult.Groups["LogLevel"].Value;
        return logLevelResult.Success && !string.IsNullOrWhiteSpace(logLevel);
    }

    internal bool TryGetConfiguration(out SelfDiagnosticsLegacyConfiguration configuration)
    {
        configuration = default;
        try
        {
            var configFilePath = this.configFileLocator();
            if (configFilePath is null)
            {
                return false;
            }

            configFilePath = Path.GetFullPath(configFilePath);

            using var file = File.Open(configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            var buffer = ArrayPool<byte>.Shared.Rent(ConfigBufferSize);
            string configJson;
            try
            {
                var totalBytesRead = 0;
                while (totalBytesRead < ConfigBufferSize)
                {
                    var bytesRead = file.Read(buffer, totalBytesRead, ConfigBufferSize - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytesRead += bytesRead;
                }

                configJson = Encoding.UTF8.GetString(buffer, 0, totalBytesRead);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (!TryParseLogDirectory(configJson, out var logDirectory)
                || !TryParseFileSize(configJson, out var fileSizeKilobytes)
                || !TryParseLogLevel(configJson, out var logLevelString)
                || !Enum.TryParse<EventLevel>(logLevelString, out var logLevel)
                || logLevel < EventLevel.LogAlways)
            {
                return false;
            }

            configuration = new SelfDiagnosticsLegacyConfiguration(
                configFilePath,
                Path.GetFullPath(logDirectory),
                fileSizeKilobytes,
                logLevel,
                logLevelString,
                formatMessageSpecified: ContainsFormatMessageKey(configJson));
            return true;
        }
        catch (Exception)
        {
            // Missing, unreadable and invalid legacy configuration (including an unusable
            // directory) stays disabled.
            return false;
        }
    }

    private static bool ContainsFormatMessageKey(string configJson)
#if NET || NETSTANDARD2_1_OR_GREATER
        => configJson.Contains("\"FormatMessage\"", StringComparison.OrdinalIgnoreCase);
#else
        => configJson.IndexOf("\"FormatMessage\"", StringComparison.OrdinalIgnoreCase) >= 0;
#endif

#if NET
    [GeneratedRegex(LogDirectoryRegexPattern, RegexOptions.IgnoreCase)]
    private static partial Regex GetLogDirectoryRegex();

    [GeneratedRegex(FileSizeRegexPattern, RegexOptions.IgnoreCase)]
    private static partial Regex GetFileSizeRegex();

    [GeneratedRegex(LogLevelRegexPattern, RegexOptions.IgnoreCase)]
    private static partial Regex GetLogLevelRegex();
#else
    private static Regex GetLogDirectoryRegex() => LogDirectoryRegex;

    private static Regex GetFileSizeRegex() => FileSizeRegex;

    private static Regex GetLogLevelRegex() => LogLevelRegex;
#endif
}
