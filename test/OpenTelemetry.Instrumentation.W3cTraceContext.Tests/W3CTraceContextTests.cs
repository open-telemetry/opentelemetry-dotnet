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
using System.IO;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenTelemetry.Trace;
#if NETCOREAPP2_1
using TestApp.AspNetCore._2._1;
#else
using TestApp.AspNetCore._3._1;
#endif
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    public class W3CTraceContextTests
        : IClassFixture<WebApplicationFactory<Startup>>, IDisposable
    {
        private readonly ITestOutputHelper output;
        private readonly WebApplicationFactory<Startup> factory;
        private TracerProvider openTelemetrySdk = null;

        public W3CTraceContextTests(ITestOutputHelper output, WebApplicationFactory<Startup> factory)
        {
            this.output = output;
            this.factory = factory;
        }

        [Fact]
        public void W3CTraceContextTestSuite()
        {
            RunCommand("git", "clone https://github.com/w3c/trace-context.git");

            // Arrange
            using (var testFactory = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(services =>
                    {
                        this.openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                            .AddAspNetCoreInstrumentation()
                            .AddHttpClientInstrumentation()
                            .Build();
                    })))
            {
                using var client = testFactory.CreateClient();

                // Act
                // Run Python script in test folder of W3C Trace Context repository
                string result = RunCommand("python", "trace-context/test/test.py http://127.0.0.1:5000/api/forward");

                // Assert
                // Assert on the last line
                // TODO: fix W3C Trace Context test suite
                // Sample failure message: "FAILED (failures=3, errors=8)"
                // string lastLine = ParseLastLine(output);
                // Assert.StartsWith("FAILED", lastLine);
                this.output.WriteLine(result);
            }
        }

        public void Dispose()
        {
            this.openTelemetrySdk?.Dispose();
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
