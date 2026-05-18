// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;

namespace Benchmarks.Exporter;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public class PrometheusAspNetCoreScrapeEndpointBenchmarks
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    private const string MeterName = "Benchmarks.Exporter.PrometheusAspNetCoreScrapeEndpoint";

    private static readonly Uri MetricsEndpoint = new("/metrics", UriKind.Relative);

    private WebApplication? app;
    private HttpClient? client;
    private Meter? meter;
    private int queueDepth = 3;

    [Params("text/plain", "application/openmetrics-text; version=1.0.0")]
    public string Accept { get; set; } = string.Empty;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();

        builder.Services.AddOpenTelemetry().WithMetrics((metrics) =>
        {
            metrics
                .AddMeter(MeterName)
                .AddPrometheusExporter((options) => options.ScrapeResponseCacheDurationMilliseconds = 0);
        });

        this.app = builder.Build();

        this.app.MapPrometheusScrapingEndpoint();

        await this.app.StartAsync().ConfigureAwait(false);

        this.client = this.app.GetTestClient();
        this.client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", this.Accept);

        this.meter = new Meter(MeterName);

        var requestCounter = this.meter.CreateCounter<long>("http.server.request.count", unit: "{request}");
        var activeRequests = this.meter.CreateUpDownCounter<long>("http.server.active_requests", unit: "{request}");
        var requestDuration = this.meter.CreateHistogram<double>("http.server.request.duration", unit: "s");
        _ = this.meter.CreateObservableGauge("worker.queue.depth", () => this.queueDepth, unit: "{item}");

        for (var i = 0; i < 50; i++)
        {
            var route = $"/resource/{i % 10}";
            var statusCode = 200 + (i % 5);
            var method = i % 2 == 0 ? "GET" : "POST";

            requestCounter.Add(
                i + 1,
                new("http.request.method", method),
                new("http.route", route),
                new("http.response.status_code", statusCode));

            activeRequests.Add(1, new("http.request.method", method), new("http.route", route));
            activeRequests.Add(-1, new("http.request.method", method), new("http.route", route));

            requestDuration.Record(
                0.005 * (i + 1),
                new("http.request.method", method),
                new("http.route", route),
                new("http.response.status_code", statusCode));
        }

        this.queueDepth = 7;

        var responseBytes = await this.client.GetByteArrayAsync(MetricsEndpoint).ConfigureAwait(false);
        if (responseBytes.Length == 0)
        {
            throw new InvalidOperationException("The Prometheus scrape endpoint returned an empty payload.");
        }
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        this.client?.Dispose();

        if (this.app != null)
        {
            await this.app.DisposeAsync().ConfigureAwait(false);
        }

        this.meter?.Dispose();
    }

    [Benchmark]
    public async Task<byte[]> ScrapeEndpoint()
        => await this.client!.GetByteArrayAsync(MetricsEndpoint).ConfigureAwait(false);
}
#endif
