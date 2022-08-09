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
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.W3cTraceContext.Tests
{
    public class W3CTraceContextTests
    {
        /*
            To run the tests, invoke docker-compose.yml from the root of the repo:
            opentelemetry>docker-compose --file=test/OpenTelemetry.Instrumentation.W3cTraceContext.Tests/docker-compose.yml --project-directory=. up --exit-code-from=tests --build
         */
        private const string W3cTraceContextEnvVarName = "OTEL_W3CTRACECONTEXT";
        private readonly HttpClient httpClient = new HttpClient();
        private readonly ITestOutputHelper output;

        public W3CTraceContextTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Trait("CategoryName", "W3CTraceContextTests")]
        [SkipUnlessEnvVarFoundTheory(W3cTraceContextEnvVarName)]
        [InlineData("placeholder")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1026:Theory methods should use all of their parameters", Justification = "Need to use SkipUnlessEnvVarFoundTheory")]
        public void W3CTraceContextTestSuiteAsync(string value)
        {
            // configure SDK
            using var tracerprovider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .Build();

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            // disabling due to failing dotnet-format
            // TODO: investigate why dotnet-format fails.
#pragma warning disable SA1008 // Opening parenthesis should be spaced correctly
            app.MapPost("/", async([FromBody]Data[] data) =>
            {
                var result = string.Empty;
                if (data != null)
                {
                    foreach (var argument in data)
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, argument.Url)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(argument.Arguments),
                                Encoding.UTF8,
                                "application/json"),
                        };
                        await this.httpClient.SendAsync(request);
                    }
                }
                else
                {
                    result = "done";
                }

                return result;
            });
#pragma warning restore SA1008 // Opening parenthesis should be spaced correctly

            app.RunAsync();

            string result = RunCommand("python", "trace-context/test/test.py http://localhost:5000/");

            // Assert
            // Assert on the last line
            // TODO: Investigate failures:
            // 1) harness sends a request with an invalid tracestate header with duplicated keys ... FAIL
            // 2) harness sends an invalid traceparent with illegal characters in trace_flags ... FAIL
            string lastLine = ParseLastLine(result);
            this.output.WriteLine("result:" + result);
            Assert.StartsWith("FAILED (failures=2)", lastLine);
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

        public class Data
        {
            [JsonPropertyName("url")]
            public string Url { get; set; }

            [JsonPropertyName("arguments")]
            public Data[] Arguments { get; set; }
        }
    }
}
