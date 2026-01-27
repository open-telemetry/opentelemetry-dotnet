// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Internal;

/// <summary>
/// Reads diagnostic configuration from OTEL_DIAGNOSTICS.json file.
/// </summary>
internal sealed partial class SelfDiagnosticsConfigParser
{
    public const string ConfigFileName = "OTEL_DIAGNOSTICS.json";
    private const int FileSizeLowerLimit = 1024;  // Lower limit for log file size in KB: 1MB
    private const int FileSizeUpperLimit = 128 * 1024;  // Upper limit for log file size in KB: 128MB

    /// <summary>
    /// ConfigBufferSize is the maximum bytes of config file that will be read.
    /// </summary>
    private const int ConfigBufferSize = 4 * 1024;

#if !NET7_0_OR_GREATER
    private static readonly Regex LogDirectoryRegex = new(
        @"""LogDirectory""\s*:\s*""(?<LogDirectory>.*?)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FileSizeRegex = new(
        @"""FileSize""\s*:\s*(?<FileSize>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LogLevelRegex = new(
        @"""LogLevel""\s*:\s*""(?<LogLevel>.*?)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FormatMessageRegex = new(
        @"""FormatMessage""\s*:\s*(?:""(?<FormatMessage>.*?)""|(?<FormatMessage>true|false))", RegexOptions.IgnoreCase | RegexOptions.Compiled);
#endif

    // This class is called in SelfDiagnosticsConfigRefresher.UpdateMemoryMappedFileFromConfiguration
    // in both main thread and the worker thread.
    // In theory the variable won't be access at the same time because worker thread first Task.Delay for a few seconds.
    private byte[]? configBuffer;

    public bool TryGetConfiguration(
        [NotNullWhen(true)] out string? logDirectory,
        out int fileSizeInKB,
        out EventLevel logLevel,
        out bool formatMessage)
    {
        logDirectory = null;
        fileSizeInKB = 0;
        logLevel = EventLevel.LogAlways;
        formatMessage = false;
        try
        {
            var configFilePath = ConfigFileName;

            // First check using current working directory
            if (!File.Exists(configFilePath))
            {
                configFilePath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);

                // Second check using application base directory
                if (!File.Exists(configFilePath))
                {
                    return false;
                }
            }

            using FileStream file = File.Open(configFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            var buffer = this.configBuffer;
            if (buffer == null)
            {
                buffer = new byte[ConfigBufferSize]; // Fail silently if OOM
                this.configBuffer = buffer;
            }

            int bytesRead = 0;
            int totalBytesRead = 0;

            while (totalBytesRead < buffer.Length)
            {
                bytesRead = file.Read(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                if (bytesRead == 0)
                {
                    break;
                }

                totalBytesRead += bytesRead;
            }

            string configJson = Encoding.UTF8.GetString(buffer, 0, totalBytesRead);

#if NET7_0_OR_GREATER
            Dictionary<string, string> configDict = new Dictionary<string, string>();
            for (Match m = JsonRegex().Match(configJson); m.Success; m = m.NextMatch())
            {
                switch (m.Groups[1].Value)
                {
                    case "LogDirectory":
                        logDirectory = m.Groups[2].Value;
                        break;
                    case "FileSize":
                        if (int.TryParse(m.Groups[2].Value, out fileSizeInKB))
                        {
                            if (fileSizeInKB < FileSizeLowerLimit)
                            {
                                fileSizeInKB = FileSizeLowerLimit;
                            }

                            if (fileSizeInKB > FileSizeUpperLimit)
                            {
                                fileSizeInKB = FileSizeUpperLimit;
                            }
                        }

                        break;
                    case "LogLevel":
                        if (!Enum.TryParse(m.Groups[2].Value, true, out logLevel))
                        {
                            return false;
                        }

                        break;
                    case "FormatMessage":
                        _ = bool.TryParse(m.Groups[2].Value, out formatMessage);
                        break;
                    default:
                        // Ignore unknown fields
                        break;
                }
            }

            return !string.IsNullOrEmpty(logDirectory) && fileSizeInKB > 0;
#else
            if (!TryParseLogDirectory(configJson, out logDirectory))
            {
                return false;
            }

            if (!TryParseFileSize(configJson, out fileSizeInKB))
            {
                return false;
            }

            if (fileSizeInKB < FileSizeLowerLimit)
            {
                fileSizeInKB = FileSizeLowerLimit;
            }

            if (fileSizeInKB > FileSizeUpperLimit)
            {
                fileSizeInKB = FileSizeUpperLimit;
            }

            if (!TryParseLogLevel(configJson, out var logLevelString))
            {
                return false;
            }

            // FormatMessage is optional, defaults to false
            _ = TryParseFormatMessage(configJson, out formatMessage);

            return Enum.TryParse(logLevelString, out logLevel);
#endif
        }
        catch (Exception)
        {
            // do nothing on failure to open/read/parse config file
            return false;
        }
    }

    /// <summary>
    /// Regex for parsing JSON into "field":"value" pairs. Will work on the flat JSONs.
    /// </summary>
#if NET7_0_OR_GREATER
    [GeneratedRegex(@"\""(\w+)\""[\s\r\n]*:[\s\r\n]*\""?([^},]*?)[""},]", RegexOptions.CultureInvariant)]
    internal static partial Regex JsonRegex();
#else
    internal static bool TryParseLogDirectory(
        string configJson,
        out string logDirectory)
    {
        var logDirectoryResult = LogDirectoryRegex.Match(configJson);
        logDirectory = logDirectoryResult.Groups["LogDirectory"].Value;
        return logDirectoryResult.Success && !string.IsNullOrWhiteSpace(logDirectory);
    }

    internal static bool TryParseFileSize(string configJson, out int fileSizeInKB)
    {
        fileSizeInKB = 0;
        var fileSizeResult = FileSizeRegex.Match(configJson);
        return fileSizeResult.Success && int.TryParse(fileSizeResult.Groups["FileSize"].Value, out fileSizeInKB);
    }

    internal static bool TryParseLogLevel(
        string configJson,
        [NotNullWhen(true)]
        out string? logLevel)
    {
        var logLevelResult = LogLevelRegex.Match(configJson);
        logLevel = logLevelResult.Groups["LogLevel"].Value;
        return logLevelResult.Success && !string.IsNullOrWhiteSpace(logLevel);
    }

    internal static bool TryParseFormatMessage(string configJson, out bool formatMessage)
    {
        formatMessage = false;
        var formatMessageResult = FormatMessageRegex.Match(configJson);
        if (formatMessageResult.Success)
        {
            var formatMessageValue = formatMessageResult.Groups["FormatMessage"].Value;
            return bool.TryParse(formatMessageValue, out formatMessage);
        }

        return true;
    }
#endif
}
