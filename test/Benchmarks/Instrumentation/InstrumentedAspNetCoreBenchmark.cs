// <copyright file="InstrumentedAspNetCoreBenchmark.cs" company="OpenTelemetry Authors">
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

#if !NETFRAMEWORK
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Benchmarks.Instrumentation
{
    [InProcess]
    [MemoryDiagnoser]
    public class InstrumentedAspNetCoreBenchmark
    {
        private const string LocalhostUrl = "http://localhost:5050";
        private const string ResourceName = "aspnetcore-service-example";
        private const string SourceName = "aspnetcore-test";

        private HttpClient client;
        private LocalServer localServer;
        private TracerProvider tracerProvider;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.localServer = new LocalServer(LocalhostUrl, true);
            this.client = new HttpClient();

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .SetResource(Resources.CreateServiceResource(ResourceName))
                .AddSource(SourceName)
                .Build();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.localServer.Dispose();
            this.client.Dispose();
            this.tracerProvider.Dispose();
        }

        [Benchmark]
        public async Task InstrumentedAspNetCoreGetPage()
        {
            var httpResponse = await this.client.GetAsync(LocalhostUrl);
            httpResponse.EnsureSuccessStatusCode();
        }
    }
}
#endif
