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

/*
// * Summary *

BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22621
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK=7.0.100-preview.6.22275.1
  [Host] : .NET 6.0.9 (6.0.922.41905), X64 RyuJIT

Job=InProcess  Toolchain=InProcessEmitToolchain

|                                      Method |     Mean |   Error |  StdDev |  Gen 0 | Allocated |
|-------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
|                 UninstrumentedAspNetCoreApp | 164.7 us | 1.66 us | 1.39 us | 0.9766 |      5 KB |
| InstrumentedAspNetCoreAppWithDefaultOptions | 178.7 us | 2.65 us | 2.35 us | 0.9766 |      5 KB |
*/

namespace Benchmarks.Instrumentation
{
    [InProcess]
    public class AspNetCoreInstrumentationBenchmarks
    {
        private HttpClient httpClient;
        private WebApplication app;
        private TracerProvider tracerProvider;

        [GlobalSetup(Target = nameof(UninstrumentedAspNetCoreApp))]
        public void UninstrumentedAspNetCoreAppGlobalSetup()
        {
            this.StartWebApplication();
            this.httpClient = new HttpClient();
        }

        [GlobalSetup(Target = nameof(InstrumentedAspNetCoreAppWithDefaultOptions))]
        public void InstrumentedAspNetCoreAppWithDefaultOptionsGlobalSetup()
        {
            this.StartWebApplication();
            this.httpClient = new HttpClient();

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .Build();
        }

        [GlobalCleanup(Target = nameof(UninstrumentedAspNetCoreApp))]
        public async Task GlobalCleanupUninstrumentedAspNetCoreAppAsync()
        {
            this.httpClient.Dispose();
            await this.app.DisposeAsync();
        }

        [GlobalCleanup(Target = nameof(InstrumentedAspNetCoreAppWithDefaultOptions))]
        public async Task GlobalCleanupInstrumentedAspNetCoreAppWithDefaultOptionsAsync()
        {
            this.httpClient.Dispose();
            await this.app.DisposeAsync();
            this.tracerProvider.Dispose();
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

        private void StartWebApplication()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddControllers();
            builder.Logging.ClearProviders();
            var app = builder.Build();
            app.MapControllers();
            app.RunAsync();

            this.app = app;
        }
    }
}
#endif
