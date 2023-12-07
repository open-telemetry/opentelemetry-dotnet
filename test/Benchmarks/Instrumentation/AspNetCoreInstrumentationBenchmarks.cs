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
.NET SDK=7.0.203
  [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2


|                     Method | EnableInstrumentation |     Mean |   Error |  StdDev |   Gen0 | Allocated |
|--------------------------- |---------------------- |---------:|--------:|--------:|-------:|----------:|
| GetRequestForAspNetCoreApp |                  None | 136.8 us | 1.56 us | 1.46 us | 0.4883 |   2.45 KB |
| GetRequestForAspNetCoreApp |                Traces | 148.1 us | 0.88 us | 0.82 us | 0.7324 |   3.57 KB |
| GetRequestForAspNetCoreApp |               Metrics | 144.4 us | 1.16 us | 1.08 us | 0.4883 |   2.92 KB |
| GetRequestForAspNetCoreApp |       Traces, Metrics | 163.0 us | 1.60 us | 1.49 us | 0.7324 |   3.63 KB |

Allocation details for .NET 7:

// Traces
* Activity creation + `Activity.Start()` = 416 B
* Casting of the struct `Microsoft.Extensions.Primitives.StringValues` to `IEnumerable<string>` by `HttpRequestHeaderValuesGetter`
  - `TraceContextPropagator.Extract` = 24 B
  - `BaggageContextPropagator.Extract` = 24 B
* String creation for `HttpRequest.HostString.Host` = 40 B
* `Activity.TagsLinkedList` (this is allocated on the first Activity.SetTag call) = 40 B
* Boxing of `Port` number when adding it as a tag = 24 B
* String creation in `GetUri` method for adding the http url tag = 66 B
* Setting `Baggage` (Setting AsyncLocal values causes allocation)
  - `BaggageHolder` creation = 24 B
  - `System.Threading.AsyncLocalValueMap.TwoElementAsyncLocalValueMap` = 48 B
  - `System.Threading.ExecutionContext` = 40 B
* `DiagNode<KeyValuePair<System.String, System.Object>>`
  - This is allocated eight times for the eight tags that are added = 8 * 40 = 320 B
* `Activity.Stop()` trying to set `Activity.Current` (This happens because of setting another AsyncLocal variable which is `Baggage`
  - System.Threading.AsyncLocalValueMap.OneElementAsyncLocalValueMap = 32 B
  - System.Threading.ExecutionContext = 40 B

Baseline = 2.45 KB
With Traces = 2.45 + (1138 / 1024) = 2.45 + 1.12 = 3.57 KB


// Metrics
* Activity creation + `Activity.Start()` = 416 B
* Boxing of `Port` number when adding it as a tag = 24 B
* String creation for `HttpRequest.HostString.Host` = 40 B

Baseline = 2.45 KB
With Metrics = 2.45 + (416 + 40 + 24) / 1024 = 2.45 + 0.47 = 2.92 KB

// With Traces and Metrics

Baseline = 2.45 KB
With Traces and Metrics = Baseline + With Traces + (With Metrics - (Activity creation + `Acitivity.Stop()`)) (they use the same activity)
                        = 2.45 + (1138 + 64) / 1024 = 2.45 + 1.17 = ~3.63KB
*/

namespace Benchmarks.Instrumentation;

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
#endif