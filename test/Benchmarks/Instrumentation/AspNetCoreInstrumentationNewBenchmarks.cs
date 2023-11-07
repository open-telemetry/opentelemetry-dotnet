// <copyright file="AspNetCoreInstrumentationNewBenchmarks.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

/*
// * Summary *

BenchmarkDotNet v0.13.10, Windows 11 (10.0.22631.2428/23H2/2023Update/SunValley3)
Intel Core i7-8850H CPU 2.60GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK 8.0.100-rc.2.23502.2
  [Host]     : .NET 8.0.0 (8.0.23.47906), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.47906), X64 RyuJIT AVX2


| Method                     | EnableInstrumentation | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------------- |---------------------- |---------:|--------:|--------:|-------:|----------:|
| GetRequestForAspNetCoreApp | None                  | 140.9 us | 2.05 us | 1.91 us | 0.4883 |   2.23 KB |
| GetRequestForAspNetCoreApp | Metri(...)brary [32]  | 147.6 us | 2.94 us | 2.75 us | 0.4883 |   2.68 KB |
| GetRequestForAspNetCoreApp | MetricsViaNet8Meters  | 150.5 us | 2.70 us | 2.53 us | 0.4883 |   2.27 KB |
*/
namespace Benchmarks.Instrumentation;

public class AspNetCoreInstrumentationNewBenchmarks
{
    private HttpClient httpClient;
    private WebApplication app;
    private MeterProvider meterProvider;

    [Flags]
    public enum EnableInstrumentationOption
    {
        /// <summary>
        /// Instrumentation is not enabled for any signal.
        /// </summary>
        None = 0,

        /// <summary>
        /// Instrumentation is enabled for Metrics via instrumentation library DiagnosticSrc.
        /// </summary>
        MetricsViaInstrumentationLibrary = 1,

        /// <summary>
        /// Instrumentation is enabled for Metrics via Meter (Enables all metrics).
        /// </summary>
        MetricsViaNet8Meters = 2,
    }

    [Params(0, 1, 2)]
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
        else if (this.EnableInstrumentation == EnableInstrumentationOption.MetricsViaInstrumentationLibrary)
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
        else if (this.EnableInstrumentation == EnableInstrumentationOption.MetricsViaNet8Meters)
        {
            this.StartWebApplication();
            this.httpClient = new HttpClient();

            var exportedItems = new List<Metric>();
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddMeter("Microsoft.AspNetCore.Http.Connections")
                .AddMeter("Microsoft.AspNetCore.Routing")
                .AddMeter("Microsoft.AspNetCore.Diagnostics")
                .AddMeter("Microsoft.AspNetCore.RateLimiting")
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
        else
        {
            this.httpClient.Dispose();
            this.app.DisposeAsync().GetAwaiter().GetResult();
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
