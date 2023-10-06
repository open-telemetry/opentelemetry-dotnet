// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
| HttpClientRequest |                  None | 161.0 us | 1.27 us | 0.99 us | 0.4883 |   2.45 KB |
| HttpClientRequest |                Traces | 173.1 us | 1.65 us | 1.54 us | 0.4883 |   4.26 KB |
| HttpClientRequest |               Metrics | 165.8 us | 1.33 us | 1.18 us | 0.7324 |   3.66 KB |
| HttpClientRequest |       Traces, Metrics | 175.6 us | 1.96 us | 1.83 us | 0.4883 |   4.28 KB |
*/

namespace Benchmarks.Instrumentation;

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
#endif
