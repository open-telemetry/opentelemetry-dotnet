// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/*
// * Summary *

BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22621.1992/22H2/2022Update/SunValley2), VM=Hyper-V
AMD EPYC 7763, 1 CPU, 16 logical and 8 physical cores
.NET SDK=7.0.306
  [Host]     : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.9 (7.0.923.32018), X64 RyuJIT AVX2


|                     Method | EnableInstrumentation |     Mean |   Error |  StdDev | Allocated |
|--------------------------- |---------------------- |---------:|--------:|--------:|----------:|
| GetRequestForAspNetCoreApp |                  None | 150.7 us | 1.68 us | 1.57 us |   2.45 KB |
| GetRequestForAspNetCoreApp |                Traces | 156.6 us | 3.12 us | 6.37 us |   3.46 KB |
| GetRequestForAspNetCoreApp |               Metrics | 148.8 us | 2.87 us | 2.69 us |   2.92 KB |
| GetRequestForAspNetCoreApp |       Traces, Metrics | 164.0 us | 3.19 us | 6.22 us |   3.52 KB |

Allocation details for .NET 7:

// Traces
* Activity creation + `Activity.Start()` = 416 B
* Casting of the struct `Microsoft.Extensions.Primitives.StringValues` to `IEnumerable<string>` by `HttpRequestHeaderValuesGetter`
  - `TraceContextPropagator.Extract` = 24 B
  - `BaggageContextPropagator.Extract` = 24 B
* String creation for `HttpRequest.HostString.Host` = 40 B
* `Activity.TagsLinkedList` (this is allocated on the first Activity.SetTag call) = 40 B
* Boxing of `Port` number when adding it as a tag = 24 B
* Setting `Baggage` (Setting AsyncLocal values causes allocation)
  - `BaggageHolder` creation = 24 B
  - `System.Threading.AsyncLocalValueMap.TwoElementAsyncLocalValueMap` = 48 B
  - `System.Threading.ExecutionContext` = 40 B
* `DiagNode<KeyValuePair<System.String, System.Object>>`
  - This is allocated seven times for the seven (eight if query string is available) tags that are added = 7 * 40 = 280 B
* `Activity.Stop()` trying to set `Activity.Current` (This happens because of setting another AsyncLocal variable which is `Baggage`
  - System.Threading.AsyncLocalValueMap.OneElementAsyncLocalValueMap = 32 B
  - System.Threading.ExecutionContext = 40 B

Baseline = 2.45 KB
With Traces = 2.45 + (1032 / 1024) = 2.45 + 1.01 = 3.46 KB


// Metrics
* Activity creation + `Activity.Start()` = 416 B
* Boxing of `Port` number when adding it as a tag = 24 B
* String creation for `HttpRequest.HostString.Host` = 40 B

Baseline = 2.45 KB
With Metrics = 2.45 + (416 + 40 + 24) / 1024 = 2.45 + 0.47 = 2.92 KB

// With Traces and Metrics

Baseline = 2.45 KB
With Traces and Metrics = Baseline + With Traces + (With Metrics - (Activity creation + `Acitivity.Stop()`)) (they use the same activity)
                        = 2.45 + (1032 + 64) / 1024 = 2.45 + 1.07 = ~3.52KB
*/
namespace Benchmarks.Instrumentation;

public class AspNetCoreInstrumentationNewBenchmarks
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
        KeyValuePair<string, string>[] config = new KeyValuePair<string, string>[] { new KeyValuePair<string, string>("OTEL_SEMCONV_STABILITY_OPT_IN", "http") };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

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
                .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
                .AddAspNetCoreInstrumentation()
                .Build();
        }
        else if (this.EnableInstrumentation == EnableInstrumentationOption.Metrics)
        {
            this.StartWebApplication();
            this.httpClient = new HttpClient();

            var exportedItems = new List<Metric>();
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                })
                .Build();
        }
        else if (this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Traces) &&
            this.EnableInstrumentation.HasFlag(EnableInstrumentationOption.Metrics))
        {
            this.StartWebApplication();
            this.httpClient = new HttpClient();

            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
                .AddAspNetCoreInstrumentation()
                .Build();

            var exportedItems = new List<Metric>();
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                })
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