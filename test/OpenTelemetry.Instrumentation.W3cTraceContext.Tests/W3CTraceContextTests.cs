// <copyright file="W3CTraceContextTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.W3cTraceContext.Tests
{
    public class W3CTraceContextTests
    {
        /*
            To run the tests, invoke docker-compose.yml from the root of the repo:
            opentelemetry>docker-compose --file=test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/docker-compose.yml --project-directory=. up --exit-code-from=tests --build

            To run as unit tests for debugging,
            1. install required softwares in the "Install" section in https://github.com/w3c/trace-context/tree/master/test,
            2. replaced the "[SkipUnlessEnvVarFoundTheory(W3cTraceContextEnvVarName)]" tag with "[Theory]",
            3. use Visual Studio to run unit tests or run with command `dotnet test`.
         */

        private const string W3cTraceContextEnvVarName = "OTEL_W3CTRACECONTEXT";
        private readonly ITestOutputHelper output;

        public W3CTraceContextTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Trait("CategoryName", "W3CTraceContextTests")]
        [SkipUnlessEnvVarFoundTheory(W3cTraceContextEnvVarName)]
        [InlineData("placeholder")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Need to use SkipUnlessEnvVarFoundTheory")]
        public void W3CTraceContextTestSuite(string value)
        {
            // Arrange
            using (var server = new InProcessServer(this.output))
            {
                // Act
                // Run Python script in test folder of W3C Trace Context repository
                string result = RunCommand("python", "trace-context/test/test.py http://127.0.0.1:5000/api/forward");

                // Assert
                // Assert on the last line
                // TODO: fix W3C Trace Context test suite
                // ASP NET Core 2.1: FAILED (failures=1)
                // ASP NET Core 3.1: FAILED (failures=3)
                // ASP NET Core 5.0: FAILED (failures=3)
                string lastLine = ParseLastLine(result);
                this.output.WriteLine("result:" + result);
                Assert.StartsWith("FAILED", lastLine);
                AssertOutputOfTestSuite(result);
            }
        }

        private static void AssertOutputOfTestSuite(string output)
        {
            string[] unsuccessfulTestcaseNames = ExtractUnsuccessfulTestcaseNames(output);

            // Tracking failures with external dependencies. https://github.com/open-telemetry/opentelemetry-dotnet/issues/1219
#if NETCOREAPP2_1
            string[] existingFailures = new string[]
            {
                // Failures:
                "test_traceparent_trace_flags_illegal_characters",  // External dependency. See issue #404
            };
#elif NETCOREAPP3_1
            string[] existingFailures = new string[]
            {
                // Failures:
                "test_traceparent_parent_id_all_zero",  // External dependency. See issue #404
                "test_traceparent_parent_id_illegal_characters",  // External dependency. See issue #404
                "test_traceparent_trace_flags_illegal_characters",  // External dependency. See issue #404
            };
#elif NET5_0
            string[] existingFailures = new string[]
            {
                // Failures:
                "test_traceparent_parent_id_all_zero",  // External dependency. See issue #404
                "test_traceparent_parent_id_illegal_characters",  // External dependency. See issue #404
                "test_traceparent_trace_flags_illegal_characters",  // External dependency. See issue #404
            };
#endif
            Assert.Equal(existingFailures.Length, unsuccessfulTestcaseNames.Length);
            for (int i = 0; i < existingFailures.Length; ++i)
            {
                Assert.Equal(existingFailures[i], unsuccessfulTestcaseNames[i]);
            }
        }

        private static string[] ExtractUnsuccessfulTestcaseNames(string output)
        {
            string[] lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Assert.True(lines != null && lines.Length != 0);
            return lines
                .Where(line => line.StartsWith("FAIL: ") || line.StartsWith("ERROR: "))
                .Select(line => line.Split(" ")[1]).ToArray();
        }

        private static string RunCommand(string command, string args)
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = $" {args}",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    WorkingDirectory = ".",
                },
            };
            proc.Start();

            var results = proc.StandardError.ReadToEnd();
            proc.WaitForExit();
            return results;
        }

        private static string ParseLastLine(string output)
        {
            if (output.Length <= 1)
            {
                return output;
            }

            // The output ends with '\n', which should be ignored.
            var lastNewLineCharacterPos = output.LastIndexOf('\n', output.Length - 2);
            return output.Substring(lastNewLineCharacterPos + 1);
        }
    }
}
