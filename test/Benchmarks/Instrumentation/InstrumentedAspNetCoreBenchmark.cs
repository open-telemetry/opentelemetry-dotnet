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
using System.Net.Http;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;

namespace Benchmarks.Instrumentation
{
    [InProcess]
    [MemoryDiagnoser]
    public class InstrumentedAspNetCoreBenchmark
    {
        private const string LocalhostUrl = "http://localhost:5050";

        private HttpClient client;
        private LocalServer localServer;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.localServer = new LocalServer(LocalhostUrl, true);
            this.client = new HttpClient();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.localServer.Dispose();
            this.client.Dispose();
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
