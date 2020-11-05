// <copyright file="UninstrumentedHttpClientBenchmark.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Tests;

namespace Benchmarks.Instrumentation
{
    [MemoryDiagnoser]
    public class UninstrumentedHttpClientBenchmark
    {
        private IDisposable serverLifeTime;
        private string url;
        private HttpClient httpClient;

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

            this.url = $"http://{host}:{port}/";
            this.httpClient = new HttpClient();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.serverLifeTime.Dispose();
            this.httpClient.Dispose();
        }

        [Benchmark]
        public async Task SimpleHttpClient()
        {
            var httpResponse = await this.httpClient.GetAsync(this.url);
            httpResponse.EnsureSuccessStatusCode();
        }
    }
}
