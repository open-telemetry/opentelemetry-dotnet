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

namespace OpenTelemetry.Internal.Tests
{
    public class SelfDiagnosticsConfigRefresherTest
    {
        private static readonly string ConfigFilePath = SelfDiagnosticsConfigParser.ConfigFileName;
        private static readonly byte[] MessageOnNewFile = SelfDiagnosticsConfigRefresher.MessageOnNewFile;

        [Fact]
        [Trait("Platform", "Any")]
        public void SelfDiagnosticsConfigRefresher_FileShare()
        {
            try
            {
                CreateConfigFile();
                using var configRefresher = new SelfDiagnosticsConfigRefresher();

                var outputFileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) + "."
                        + Process.GetCurrentProcess().Id + ".log";
                var outputFilePath = Path.Combine(".", outputFileName);
                using var file = File.Open(outputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] actualBytes = new byte[MessageOnNewFile.Length];
                file.Read(actualBytes, 0, actualBytes.Length);
                Assert.Equal(MessageOnNewFile, actualBytes);
            }
            finally
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
    }
}
