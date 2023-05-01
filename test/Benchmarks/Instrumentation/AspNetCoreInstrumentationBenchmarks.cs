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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/*
BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.23424.1000)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.203
  [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2


|                     Method | EnableInstrumentation |     Mean |   Error |  StdDev |   Gen0 | Allocated |
|--------------------------- |---------------------- |---------:|--------:|--------:|-------:|----------:|
| GetRequestForAspNetCoreApp |                  None | 226.8 us | 4.00 us | 3.74 us |      - |   2.45 KB |
| GetRequestForAspNetCoreApp |                Traces | 235.2 us | 4.44 us | 4.15 us | 0.4883 |   3.59 KB |
| GetRequestForAspNetCoreApp |               Metrics | 229.1 us | 4.44 us | 4.36 us |      - |   2.92 KB |
| GetRequestForAspNetCoreApp |       Traces, Metrics | 230.6 us | 4.54 us | 5.23 us | 0.4883 |   3.66 KB |
*/

namespace Benchmarks.Instrumentation
{
    public class AspNetCoreInstrumentationBenchmarks
    {
        private HttpClient httpClient;
        private WebApplication app;
        private TracerProvider tracerProvider;
        private MeterProvider meterProvider;

        [Flags]
        public enum EnableInstrumentationOption
        {
            /// <summary>
            /// Instrumentation is not enabled for any signal.
            /// </summary>
            None = 0,

            /// <summary>
            /// Instrumentation is enbled only for Traces.
            /// </summary>
            Traces = 1,

            /// <summary>
            /// Instrumentation is enbled only for Metrics.
            /// </summary>
            Metrics = 2,
        }

        [Params(0, 1, 2, 3)]
        public EnableInstrumentationOption EnableInstrumentation { get; set; }

        [GlobalSetup(Target = nameof(GetRequestForAspNetCoreApp))]
        public void GetRequestForAspNetCoreAppGlobalSetup()
        {
            if (this.EnableInstrumentation == EnableInstrumentationOption.None)
            {
                this.StartWebApplication();
                this.httpClient = new HttpClient();
            }
            else if (this.EnableInstrumentation == EnableInstrumentationOption.Traces)
            {
                this.StartWebApplication();
                this.httpClient = new HttpClient();

                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .Build();
            }
            else if (this.EnableInstrumentation == EnableInstrumentationOption.Metrics)
            {
                this.StartWebApplication();
                this.httpClient = new HttpClient();

                this.meterProvider = Sdk.CreateMeterProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .Build();
            }
            else if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Traces) &&
                this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Metrics))
            {
                this.StartWebApplication();
                this.httpClient = new HttpClient();

                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .Build();

                this.meterProvider = Sdk.CreateMeterProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .Build();
            }
        }

        [GlobalCleanup(Target = nameof(GetRequestForAspNetCoreApp))]
        public void GetRequestForAspNetCoreAppGlobalCleanup()
        {
            if (this.EnableInstrumentation == EnableInstrumentationOption.None)
            {
                this.httpClient.Dispose();
                this.app.DisposeAsync().GetAwaiter().GetResult();
            }
            else if (this.EnableInstrumentation == EnableInstrumentationOption.Traces)
            {
                this.httpClient.Dispose();
                this.app.DisposeAsync().GetAwaiter().GetResult();
                this.tracerProvider.Dispose();
            }
            else if (this.EnableInstrumentation == EnableInstrumentationOption.Metrics)
            {
                this.httpClient.Dispose();
                this.app.DisposeAsync().GetAwaiter().GetResult();
                this.meterProvider.Dispose();
            }
            else if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Traces) &&
                this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Metrics))
            {
                this.httpClient.Dispose();
                this.app.DisposeAsync().GetAwaiter().GetResult();
                this.tracerProvider.Dispose();
                this.meterProvider.Dispose();
            }
        }

        [Benchmark]
        public async Task GetRequestForAspNetCoreApp()
        {
            var httpResponse = await this.httpClient.GetAsync("http://localhost:5000").ConfigureAwait(false);
            httpResponse.EnsureSuccessStatusCode();
        }

        private void StartWebApplication()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            var app = builder.Build();
            app.MapGet("/", async context => await context.Response.WriteAsync($"Hello World!"));
            app.RunAsync();

            this.app = app;
        }
    }
}
#endif
