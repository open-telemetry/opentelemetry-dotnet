// <copyright file="InstrumentedHttpClientBenchmark.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace Benchmarks.Instrumentation
{
    [MemoryDiagnoser]
    public class InstrumentedHttpClientBenchmark
    {
        private const string ActivityName = "incoming request";
        private const string ServiceName = "http-service-example";
        private const string SourceName = "http-client-test";

        private HttpClient httpClient;
        private TracerProvider tracerProvider;
        private IDisposable serverLifeTime;
        private ActivitySource source;
        private string url;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.serverLifeTime = TestHttpServer.RunServer(
                (ctx) =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.Close();
                },
                out var host,
                out var port);

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName))
                .AddSource(SourceName)
                .Build();

            this.url = $"http://{host}:{port}/";
            this.httpClient = new HttpClient();
            this.source = new ActivitySource(SourceName);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.httpClient.Dispose();
            this.tracerProvider.Dispose();
            this.serverLifeTime.Dispose();
            this.source.Dispose();
        }

        [Benchmark]
        public async Task InstrumentedHttpClient()
        {
            var httpResponse = await this.httpClient.GetAsync(this.url);
            httpResponse.EnsureSuccessStatusCode();
        }

        [Benchmark]
        public async Task InstrumentedHttpClientWithParentActivity()
        {
            using var parent = this.source.StartActivity(ActivityName, ActivityKind.Server);
            var httpResponse = await this.httpClient.GetAsync(this.url);
            httpResponse.EnsureSuccessStatusCode();
        }
    }
}
