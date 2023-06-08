// <copyright file="HttpClientInstrumentationBenchmarks.cs" company="OpenTelemetry Authors">
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
BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22621.1702/22H2/2022Update/SunValley2)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK=7.0.302
  [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2


|            Method | EnableInstrumentation |     Mean |   Error |  StdDev |   Gen0 | Allocated |
|------------------ |---------------------- |---------:|--------:|--------:|-------:|----------:|
| HttpClientRequest |                  None | 159.8 us | 1.29 us | 1.14 us | 0.4883 |   2.45 KB |
| HttpClientRequest |                Traces | 170.8 us | 1.60 us | 1.49 us | 0.4883 |   4.31 KB |
| HttpClientRequest |               Metrics | 164.1 us | 0.99 us | 0.77 us | 0.7324 |   3.71 KB |
| HttpClientRequest |       Traces, Metrics | 173.5 us | 1.12 us | 1.05 us | 0.4883 |   4.34 KB |
*/

namespace Benchmarks.Instrumentation
{
    public class HttpClientInstrumentationBenchmarks
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

        [GlobalSetup(Target = nameof(HttpClientRequest))]
        public void HttpClientRequestGlobalSetup()
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
                    .AddHttpClientInstrumentation()
                    .Build();
            }
            else if (this.EnableInstrumentation == EnableInstrumentationOption.Metrics)
            {
                this.StartWebApplication();
                this.httpClient = new HttpClient();

                this.meterProvider = Sdk.CreateMeterProviderBuilder()
                    .AddHttpClientInstrumentation()
                    .Build();
            }
            else if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Traces) &&
                this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Metrics))
            {
                this.StartWebApplication();
                this.httpClient = new HttpClient();

                this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddHttpClientInstrumentation()
                    .Build();

                this.meterProvider = Sdk.CreateMeterProviderBuilder()
                    .AddHttpClientInstrumentation()
                    .Build();
            }
        }

        [GlobalCleanup(Target = nameof(HttpClientRequest))]
        public void HttpClientRequestGlobalCleanup()
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
        public async Task HttpClientRequest()
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
