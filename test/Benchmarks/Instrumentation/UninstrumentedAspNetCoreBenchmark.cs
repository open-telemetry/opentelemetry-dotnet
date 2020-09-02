// <copyright file="UninstrumentedAspNetCoreBenchmark.cs" company="OpenTelemetry Authors">
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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
#if NETCOREAPP2_1
using TestApp.AspNetCore._2._1;
#else
using TestApp.AspNetCore._3._1;
#endif

namespace Benchmarks.Instrumentation
{
    [InProcess]
    [MemoryDiagnoser]
    public class UninstrumentedAspNetCoreBenchmark
    {
        private HttpClient client;
        private WebApplicationFactory<Startup> factory;

        [GlobalSetup]
        public void GlobalSetup()
        {
            static void ConfigureTestServices(IServiceCollection services)
            {
            }

            this.factory = new WebApplicationFactory<Startup>()
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices));

            this.client = this.factory.CreateClient();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.client.Dispose();
            this.factory.Dispose();
        }

        [Benchmark]
        public async Task SimpleHttpClient()
        {
            var httpResponse = await this.client.GetAsync("/api/values");
            httpResponse.EnsureSuccessStatusCode();
        }
    }
}
#endif
