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
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/*
// * Summary *

BenchmarkDotNet=v0.13.3, OS=Windows 10 (10.0.19045.2604)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=7.0.103
  [Host] : .NET 7.0.3 (7.0.323.6910), X64 RyuJIT AVX2

Job=InProcess  Toolchain=InProcessEmitToolchain

|                                      Method |     Mean |   Error |  StdDev |   Gen0 | Allocated |
|-------------------------------------------- |---------:|--------:|--------:|-------:|----------:|
|                 UninstrumentedAspNetCoreApp | 149.4 us | 2.94 us | 2.75 us | 0.4883 |   2.54 KB |
| InstrumentedAspNetCoreAppWithDefaultOptions | 171.9 us | 2.65 us | 2.48 us | 0.7324 |   3.79 KB |
*/

namespace Benchmarks.Instrumentation
{
    [InProcess]
    public class AspNetCoreInstrumentationBenchmarks
    {
        private HttpClient httpClient;
        private WebApplication app;
        private TracerProvider tracerProvider;
        private MeterProvider meterProvider;

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

            var exportedItems = new List<Metric>();
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();
        }

        [GlobalCleanup(Target = nameof(UninstrumentedAspNetCoreApp))]
        public async Task GlobalCleanupUninstrumentedAspNetCoreAppAsync()
        {
            this.httpClient.Dispose();
            await this.app.DisposeAsync().ConfigureAwait(false);
        }

        [GlobalCleanup(Target = nameof(InstrumentedAspNetCoreAppWithDefaultOptions))]
        public async Task GlobalCleanupInstrumentedAspNetCoreAppWithDefaultOptionsAsync()
        {
            this.httpClient.Dispose();
            await this.app.DisposeAsync().ConfigureAwait(false);
            this.tracerProvider.Dispose();
            this.meterProvider.Dispose();
        }

        [Benchmark]
        public async Task UninstrumentedAspNetCoreApp()
        {
            var httpResponse = await this.httpClient.GetAsync("http://localhost:5000").ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
        }

        [Benchmark]
        public async Task InstrumentedAspNetCoreAppWithDefaultOptions()
        {
            var httpResponse = await this.httpClient.GetAsync("http://localhost:5000").ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
        }

        private void StartWebApplication()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            var app = builder.Build();
            app.MapGet("/", () => $"Hello World!");
            app.RunAsync();

            this.app = app;
        }
    }
}
#endif
