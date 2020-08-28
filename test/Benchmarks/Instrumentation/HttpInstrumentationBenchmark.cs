// <copyright file="HttpInstrumentationBenchmark.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Benchmarks.Instrumentation
{
    [MemoryDiagnoser]
    public class HttpInstrumentationBenchmark
    {
        private HttpClient httpClient;
        private ActivitySource source;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.httpClient = new HttpClient();
            this.source = new ActivitySource("http-client-test");
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.httpClient.Dispose();
            this.source.Dispose();
        }

        [Benchmark(Baseline = true)]
        public async Task SimpleHttpClient()
        {
            var httpResponse = await this.httpClient.GetAsync("https://opentelemetry.io/");
            httpResponse.EnsureSuccessStatusCode();
        }

        [Benchmark]
        public async Task InstrumentedHttpClient()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .SetResource(Resources.CreateServiceResource("http-service-example"))
                .AddSource("http-client-test")
                .Build();

            var httpResponse = await this.httpClient.GetAsync("https://opentelemetry.io/");
            httpResponse.EnsureSuccessStatusCode();
        }

        [Benchmark]
        public async Task InstrumentedHttpClientWithParentActivity()
        {
            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .SetResource(Resources.CreateServiceResource("http-service-example"))
                .AddSource("http-client-test")
                .Build();

            using var parent = this.source.StartActivity("incoming request", ActivityKind.Server);
            var httpResponse = await this.httpClient.GetAsync("https://opentelemetry.io/");
            httpResponse.EnsureSuccessStatusCode();
        }
    }
}
