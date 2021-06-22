// <copyright file="SelfDiagnosticsConfigParserTest.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Internal.Tests
{
    public class SelfDiagnosticsConfigParserTest
    {
        [Fact]
        public void SelfDiagnosticsConfigParser_TryParseFilePath_Success()
        {
            string configJson = "{ \t \n "
                                + "\t    \"LogDirectory\" \t : \"Diagnostics\", \n"
                                + "FileSize \t : \t \n"
                                + " 1024 \n}\n";
            Assert.True(SelfDiagnosticsConfigParser.TryParseLogDirectory(configJson, out string logDirectory));
            Assert.Equal("Diagnostics", logDirectory);
        }

        [Fact]
        public void SelfDiagnosticsConfigParser_TryParseFilePath_MissingField()
        {
            string configJson = @"{
                    ""path"": ""Diagnostics"",
                    ""FileSize"": 1024
                    }";
            Assert.False(SelfDiagnosticsConfigParser.TryParseLogDirectory(configJson, out string logDirectory));
        }

        [Fact]
        public void SelfDiagnosticsConfigParser_TryParseFileSize()
        {
            string configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""FileSize"": 1024
                    }";
            Assert.True(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out int fileSize));
            Assert.Equal(1024, fileSize);
        }

        [Fact]
        public void SelfDiagnosticsConfigParser_TryParseFileSize_CaseInsensitive()
        {
            string configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""fileSize"" :
                                   2048
                    }";
            Assert.True(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out int fileSize));
            Assert.Equal(2048, fileSize);
        }

        [Fact]
        public void SelfDiagnosticsConfigParser_TryParseFileSize_MissingField()
        {
            string configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""size"": 1024
                    }";
            Assert.False(SelfDiagnosticsConfigParser.TryParseFileSize(configJson, out int fileSize));
        }

        [Fact]
        public void SelfDiagnosticsConfigParser_TryParseLogLevel()
        {
            string configJson = @"{
                    ""LogDirectory"": ""Diagnostics"",
                    ""FileSize"": 1024,
                    ""LogLevel"": ""Error""
                    }";
            Assert.True(SelfDiagnosticsConfigParser.TryParseLogLevel(configJson, out string logLevelString));
            Assert.Equal("Error", logLevelString);
        }
    }
}
