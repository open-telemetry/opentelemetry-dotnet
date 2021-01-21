// <copyright file="SelfDiagnosticsConfigRefresherTest.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Internal.Tests
{
    public class SelfDiagnosticsConfigRefresherTest
    {
        private static readonly string ConfigFilePath = SelfDiagnosticsConfigParser.ConfigFileName;
        private static readonly byte[] MessageOnNewFile = SelfDiagnosticsConfigRefresher.MessageOnNewFile;
        private static readonly string MessageOnNewFileString = Encoding.UTF8.GetString(SelfDiagnosticsConfigRefresher.MessageOnNewFile);

        private readonly ITestOutputHelper output;

        public SelfDiagnosticsConfigRefresherTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void SelfDiagnosticsConfigRefresher_OmitAsConfigured()
        {
            try
            {
                CreateConfigFile();
                using var configRefresher = new SelfDiagnosticsConfigRefresher();

                // Emitting event of EventLevel.Warning
                OpenTelemetrySdkEventSource.Log.SpanProcessorQueueIsExhausted();

                int bufferSize = 512;
                byte[] actualBytes = ReadFile(bufferSize);
                string logText = Encoding.UTF8.GetString(actualBytes);
                this.output.WriteLine(logText);  // for debugging in case the test fails
                Assert.StartsWith(MessageOnNewFileString, logText);

                // The event was omitted
                Assert.Equal('\0', (char)actualBytes[MessageOnNewFile.Length]);
            }
            finally
            {
                CleanupConfigFile();
            }
        }

        [Fact]
        [Trait("Platform", "Any")]
        public void SelfDiagnosticsConfigRefresher_CaptureAsConfigured()
        {
            try
            {
                CreateConfigFile();
                using var configRefresher = new SelfDiagnosticsConfigRefresher();

                // Emitting event of EventLevel.Error
                OpenTelemetrySdkEventSource.Log.SpanProcessorException("Event string sample", "Exception string sample");

                int bufferSize = 512;
                byte[] actualBytes = ReadFile(bufferSize);
                string logText = Encoding.UTF8.GetString(actualBytes);
                Assert.StartsWith(MessageOnNewFileString, logText);

                // The event was captured
                string logLine = logText.Substring(MessageOnNewFileString.Length);
                string logMessage = ParseLogMessage(logLine);
                string expectedMessage = "Unknown error in SpanProcessor event '{0}': '{1}'.{Event string sample}{Exception string sample}";
                Assert.StartsWith(expectedMessage, logMessage);
            }
            finally
            {
                CleanupConfigFile();
            }
        }

        private static string ParseLogMessage(string logLine)
        {
            int timestampPrefixLength = "2020-08-14T20:33:24.4788109Z:".Length;
            Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z:", logLine.Substring(0, timestampPrefixLength));
            return logLine.Substring(timestampPrefixLength);
        }

        private static byte[] ReadFile(int byteCount)
        {
            var outputFileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) + "."
                    + Process.GetCurrentProcess().Id + ".log";
            var outputFilePath = Path.Combine(".", outputFileName);
            using var file = File.Open(outputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] actualBytes = new byte[byteCount];
            file.Read(actualBytes, 0, byteCount);
            return actualBytes;
        }

        private static void CreateConfigFile()
        {
            string configJson = @"{
                    ""LogDirectory"": ""."",
                    ""FileSize"": 1024,
                    ""LogLevel"": ""Error""
                    }";
            using FileStream file = File.Open(ConfigFilePath, FileMode.Create, FileAccess.Write);
            byte[] configBytes = Encoding.UTF8.GetBytes(configJson);
            file.Write(configBytes, 0, configBytes.Length);
        }

        private static void CleanupConfigFile()
        {
            try
            {
                File.Delete(ConfigFilePath);
            }
            catch
            {
            }
        }
    }
}
