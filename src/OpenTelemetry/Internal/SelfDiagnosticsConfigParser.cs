// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Text;

namespace OpenTelemetry.Internal;

internal sealed class SelfDiagnosticsConfigParser
{
    public const string ConfigFileName = "OTEL_DIAGNOSTICS.json";
    private const int FileSizeLowerLimit = 1024;  // Lower limit for log file size in KB: 1MB
    private const int FileSizeUpperLimit = 128 * 1024;  // Upper limit for log file size in KB: 128MB

    /// <summary>
    /// ConfigBufferSize is the maximum bytes of config file that will be read.
    /// </summary>
    private const int ConfigBufferSize = 4 * 1024;

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
            var config = ParseConfigFile(configJson);

            if (!config.TryGetValue("LogDirectory", out logDirectory))
            {
                return false;
            }

            if (!config.TryGetValue("FileSize", out string? fileSizeStr) || !int.TryParse(fileSizeStr, out fileSizeInKB))
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

            if (!config.TryGetValue("LogLevel", out var logLevelString))
            {
                return false;
            }

            // FormatMessage is optional, defaults to false
            if (config.TryGetValue("FormatMessage", out string? formatMessageStr))
            {
                _ = bool.TryParse(formatMessageStr, out formatMessage);
            }

            return Enum.TryParse(logLevelString, out logLevel);
        }
        catch (Exception)
        {
            // do nothing on failure to open/read/parse config file
            return false;
        }
    }

    internal static Dictionary<string, string> ParseConfigFile(string content)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        int pos = 0;

        SkipVoid(content, ref pos);
        ReadSymbol(content, '{', ref pos);
        while (pos < content.Length)
        {
            string fieldName = ReadToken(content, ref pos);
            ReadSymbol(content, ':', ref pos);
            string value = ReadToken(content, ref pos);
            result.Add(fieldName, value);
            if (content[pos] == '}')
            {
                break;
            }
            else
            {
                pos++;
            }
        }

        ReadSymbol(content, '}', ref pos);
        return result;

        static bool ReadSymbol(string content, char target, ref int pos)
        {
            if (pos < content.Length && content[pos] == target)
            {
                pos++;
                return true;
            }
            else
            {
                throw new FormatException("Invalid JSON data in " + ConfigFileName);
            }
        }

        static void SkipVoid(string content, ref int pos)
        {
            while (pos < content.Length && char.IsWhiteSpace(content[pos]))
            {
                pos++;
            }
        }

        static string ReadToken(string content, ref int pos)
        {
            SkipVoid(content, ref pos);
            int start = pos;
            ReadOnlySpan<char> endings = stackalloc char[] { ':', ',', '}', '\n', '\r' };
            if (content[pos] == '"') // if token is quoted
            {
                pos++;
                while (pos < content.Length && content[pos] != '"')
                {
                    pos++;
                }

                int end = pos++;
                SkipVoid(content, ref pos);
                return content.Substring(start + 1, end - start - 1);
            }
            else
            {
                while (pos < content.Length && endings.IndexOf(content[pos]) == -1)
                {
                    pos++;
                }

                int end = pos;
                SkipVoid(content, ref pos);
                return content.Substring(start, pos - start);
            }
        }
    }
}
