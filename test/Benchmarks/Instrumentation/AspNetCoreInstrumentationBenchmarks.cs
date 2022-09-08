// <copyright file="AspNetCoreInstrumentationBenchmarks.cs" company="OpenTelemetry Authors">
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
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Benchmarks.Instrumentation
{
    [InProcess]
    public class AspNetCoreInstrumentationBenchmarks
    {
        private HttpClient httpClient;
        private WebApplication app;

        [GlobalSetup(Target = nameof(UninstrumentedAspNetCoreApp))]
        public void UninstrumentedAspNetCoreAppGlobalSetup()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers();
            builder.Logging.ClearProviders();
            var app = builder.Build();
            app.MapControllers();
            app.RunAsync();

            this.app = app;
            this.httpClient = new HttpClient();
        }

        [GlobalSetup(Target = nameof(InstrumentedAspNetCoreAppWithDefaultOptions))]
        public void InstrumentedAspNetCoreAppWithDefaultOptionsGlobalSetup()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers();
            builder.Logging.ClearProviders();
            var app = builder.Build();
            app.MapControllers();
            app.RunAsync();

            this.app = app;
            this.httpClient = new HttpClient();

            Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .Build();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.httpClient.Dispose();
            this.app.DisposeAsync();
        }

        [Benchmark]
        public async Task UninstrumentedAspNetCoreApp()
        {
            var httpResponse = await this.httpClient.GetAsync("http://localhost:5000/api/values");
            httpResponse.EnsureSuccessStatusCode();
        }

        [Benchmark]
        public async Task InstrumentedAspNetCoreAppWithDefaultOptions()
        {
            var httpResponse = await this.httpClient.GetAsync("http://localhost:5000/api/values");
            httpResponse.EnsureSuccessStatusCode();
        }
    }
}
#endif
