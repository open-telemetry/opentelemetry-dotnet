// <copyright file="SelfDiagnosticsConfigParser.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenTelemetry.Internal
{
    internal class SelfDiagnosticsConfigParser
    {
        public const string ConfigFileName = "OTEL_DIAGNOSTICS.json";
        private const int FileSizeLowerLimit = 1024;  // Lower limit for log file size in KB: 1MB
        private const int FileSizeUpperLimit = 128 * 1024;  // Upper limit for log file size in KB: 128MB

        /// <summary>
        /// ConfigBufferSize is the maximum bytes of config file that will be read.
        /// </summary>
        private const int ConfigBufferSize = 4 * 1024;

        private static readonly Regex LogDirectoryRegex = new Regex(
            @"""LogDirectory""\s*:\s*""(?<LogDirectory>.*?)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FileSizeRegex = new Regex(
            @"""FileSize""\s*:\s*(?<FileSize>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LogLevelRegex = new Regex(
            @"""LogLevel""\s*:\s*""(?<LogLevel>.*?)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // This class is called in SelfDiagnosticsConfigRefresher.UpdateMemoryMappedFileFromConfiguration
        // in both main thread and the worker thread.
        // In theory the variable won't be access at the same time because worker thread first Task.Delay for a few seconds.
        private byte[] configBuffer;

        public bool TryGetConfiguration(out string logDirectory, out int fileSizeInKB, out EventLevel logLevel)
        {
            logDirectory = null;
            fileSizeInKB = 0;
            logLevel = EventLevel.LogAlways;
            try
            {
                if (!File.Exists(ConfigFileName))
                {
                    return false;
                }

                using FileStream file = File.Open(ConfigFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var buffer = this.configBuffer;
                if (buffer == null)
                {
                    buffer = new byte[ConfigBufferSize]; // Fail silently if OOM
                    this.configBuffer = buffer;
                }

                file.Read(buffer, 0, buffer.Length);
                string configJson = Encoding.UTF8.GetString(buffer);
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

                logLevel = (EventLevel)Enum.Parse(typeof(EventLevel), logLevelString);
                return true;
            }
            catch (Exception)
            {
                // do nothing on failure to open/read/parse config file
            }

            return false;
        }

        internal static bool TryParseLogDirectory(string configJson, out string logDirectory)
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

        internal static bool TryParseLogLevel(string configJson, out string logLevel)
        {
            var logLevelResult = LogLevelRegex.Match(configJson);
            logLevel = logLevelResult.Groups["LogLevel"].Value;
            return logLevelResult.Success && !string.IsNullOrWhiteSpace(logLevel);
        }
    }
}
