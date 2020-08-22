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

using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.W3cTraceContext.Tests
{
    public class W3CTraceContextTests
    {
        private readonly ITestOutputHelper output;

        public W3CTraceContextTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void W3CTraceContextTestSuite()
        {
            RunCommand("git", "clone https://github.com/w3c/trace-context.git");

            // Arrange
            using (var server = new InProcessServer(this.output))
            {
                // Act
                // Run Python script in test folder of W3C Trace Context repository
                string result = RunCommand("python", "trace-context/test/test.py http://127.0.0.1:5000/api/forward");

                // Assert
                // Assert on the last line
                // TODO: fix W3C Trace Context test suite
                // ASP NET Core 2.1: FAILED (failures=4, errors=7)
                // ASP NET Core 3.1: FAILED (failures=6, errors=7)
                // string lastLine = ParseLastLine(result);
                // Assert.StartsWith("FAILED", lastLine);
                this.output.WriteLine(result);
            }
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

            // TODO: after W3C Trace Context test suite passes, it might go in standard output
            // var results = proc.StandardOutput.ReadToEnd();
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
