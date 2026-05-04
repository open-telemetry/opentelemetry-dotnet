// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter.Prometheus.Tests;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Exporter.Prometheus.AspNetCore.Tests;

[Collection(PromToolCollection.Name)]
public class PrometheusIntegrationTests(PromToolFixture promtool, ITestOutputHelper outputHelper)
{
    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("")]
    [InlineData("OpenMetricsText0.0.1")]
    [InlineData("OpenMetricsText1.0.0")]
    [InlineData("PrometheusText0.0.4")]
    [InlineData("PrometheusText1.0.0")]

    public async Task Prometheus_Can_Scrape_Metrics(string scrapeProtocol) => await GenerateMetricsAsync(async (baseAddress) =>
    {
        // Arrange
        var prometheus = new PrometheusFixture()
        {
            TargetPort = baseAddress.Port,
        };

        if (!string.IsNullOrEmpty(scrapeProtocol))
        {
            prometheus.ScrapeProtocols = [scrapeProtocol];
        }

        try
        {
            // Act
            await prometheus.StartAsync();

            var prometheusBaseAddress = prometheus.GetBaseAddress(9090);

            await WaitForServiceDiscoveryAsync(prometheusBaseAddress);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            IReadOnlyList<string> series = [];

            // Assert
            while (!cts.IsCancellationRequested)
            {
                series = await WaitForMetricsSeriesAsync(prometheusBaseAddress);

                if (series.Contains("temperature_celsius"))
                {
                    break;
                }
            }

            Assert.Contains("aspnetcore_memory_pool_allocated_bytes_total", series);
            Assert.Contains("http_server_active_requests", series);
            Assert.Contains("http_server_request_duration_seconds_bucket", series);
            Assert.Contains("http_server_request_duration_seconds_count", series);
            Assert.Contains("http_server_request_duration_seconds_sum", series);
            Assert.Contains("kestrel_active_connections", series);
            Assert.Contains("kestrel_connection_duration_seconds_bucket", series);
            Assert.Contains("kestrel_connection_duration_seconds_count", series);
            Assert.Contains("kestrel_connection_duration_seconds_sum", series);
            Assert.Contains("processed_bytes_total", series);
            Assert.Contains("queue_balance", series);
            Assert.Contains("temperature_celsius", series);
        }
        finally
        {
            await prometheus.DisposeAsync();
        }

        static async Task<IReadOnlyList<string>> WaitForMetricsSeriesAsync(Uri baseAddress)
        {
            // See https://prometheus.io/docs/prometheus/latest/querying/api/#finding-series-by-label-matchers
            var seriesUrl = QueryHelpers.AddQueryString(
                "/api/v1/series",
                [
                    KeyValuePair.Create<string, string?>("limit", "0"),
                    KeyValuePair.Create<string, string?>("match[]", "{job=\"prometheus-target\"}"),
                ]);

            var seriesUri = new Uri(seriesUrl, UriKind.Relative);

            var frequency = TimeSpan.FromMilliseconds(250);
            using var client = new HttpClient() { BaseAddress = baseAddress };
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    using var metrics = await client.GetFromJsonAsync<JsonDocument>(seriesUri, cts.Token);

                    if (metrics!.RootElement.ValueKind is JsonValueKind.Object &&
                        metrics.RootElement.TryGetProperty("status", out var status) &&
                        status.GetString() == "success")
                    {
                        var data = metrics.RootElement.GetProperty("data");

                        if (data.GetArrayLength() > 0)
                        {
                            var series = new HashSet<string>();

                            foreach (var seriesElement in data.EnumerateArray())
                            {
                                if (seriesElement.ValueKind is JsonValueKind.Object &&
                                    seriesElement.TryGetProperty("__name__", out var name))
                                {
                                    series.Add(name.GetString()!);
                                }
                            }

                            return [.. series];
                        }
                    }
                }
                catch (Exception)
                {
                    await Task.Delay(frequency);
                }
            }

            cts.Token.ThrowIfCancellationRequested();
            return [];
        }

        static async Task WaitForServiceDiscoveryAsync(Uri baseAddress)
        {
            // See https://prometheus.io/docs/prometheus/latest/querying/api/#targets
            using var client = new HttpClient() { BaseAddress = baseAddress };
            var targetsUri = new Uri("/api/v1/targets", UriKind.Relative);

            var frequency = TimeSpan.FromMilliseconds(250);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    using var targets = await client.GetFromJsonAsync<JsonDocument>(targetsUri, cts.Token);

                    if (targets!.RootElement.ValueKind is JsonValueKind.Object &&
                        targets.RootElement.TryGetProperty("status", out var status) &&
                        status.GetString() == "success")
                    {
                        var activeTargets = targets.RootElement
                            .GetProperty("data")
                            .GetProperty("activeTargets");

                        if (activeTargets.GetArrayLength() > 0)
                        {
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    await Task.Delay(frequency);
                }
            }

            cts.Token.ThrowIfCancellationRequested();
        }
    });

    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("")]
    [InlineData("text/plain")]
    [InlineData("text/plain;version=0.0.4")]
    [InlineData("text/plain;version=1.0.0")]
    [InlineData("application/openmetrics-text", Skip = "https://github.com/prometheus/prometheus/issues/8932")]
    [InlineData("application/openmetrics-text;version=0.0.4")]
    [InlineData("application/openmetrics-text;version=1.0.0", Skip = "https://github.com/prometheus/prometheus/issues/8932")]
    [InlineData("application/openmetrics-text;version=1.0.0;escaping=allow-utf-8;q=0.5,application/openmetrics-text;version=0.0.1;q=0.4,text/plain;version=1.0.0;escaping=allow-utf-8;q=0.3,text/plain;version=0.0.4;q=0.2,/;q=0.1", Skip = "https://github.com/prometheus/prometheus/issues/8932")]

    public async Task Promtool_Considers_Scrape_Response_Valid(string accept) => await GenerateMetricsAsync(async (baseAddress) =>
    {
        // Act
        var actual = await promtool.CheckMetricsAsync(new(baseAddress, "metrics"), accept);

        outputHelper.WriteLine($"[promtool] ExitCode: {actual.ExitCode}");
        outputHelper.WriteLine("[promtool] stdout:");
        outputHelper.WriteLine(string.Empty);
        outputHelper.WriteLine(actual.Stdout);

        if (!string.IsNullOrEmpty(actual.Stderr))
        {
            outputHelper.WriteLine(string.Empty);
            outputHelper.WriteLine("[promtool] stderr:");
            outputHelper.WriteLine(string.Empty);
            outputHelper.WriteLine(actual.Stderr);
        }

        // Assert
        Assert.Equal(0, actual.ExitCode);
        Assert.NotEmpty(actual.Stdout);
        Assert.Empty(actual.Stderr);
    });

    private static async Task GenerateMetricsAsync(
        Func<Uri, Task> actAndAssert)
    {
        // Arrange
        const string meterName = "prometheus.integration.tests";
        const string meterVersion = "1.2.3";

        using var meter = new Meter(
            meterName,
            meterVersion,
            [new("meter_tag", "meter-value")]);

        var counter = meter.CreateCounter<long>("kestrel.rejected_connections", unit: "{connection}", description: "Number of connections rejected by the server.");
        var upDownCounter = meter.CreateUpDownCounter<long>("kestrel.active_connections", unit: "{connection}", description: "Number of connections that are currently active on the server.");
        var histogram = meter.CreateHistogram<double>("kestrel.connection.duration", unit: "s", description: "The duration of connections on the server.");

        var ignoredHistogram = meter.CreateHistogram<double>("exponential_latency", unit: "ms", description: "Ignored exponential histogram.");

        meter.CreateObservableCounter(
            "processed_bytes",
            () => new Measurement<long>(4, [new("source", "scheduler")]),
            unit: "bytes",
            description: "Background processed bytes.");

        meter.CreateObservableGauge(
            "temperature",
            () => new Measurement<double>(22.5, [new("region", "eu-west-1")]),
            unit: "celsius",
            description: "Current temperature.");

        meter.CreateObservableUpDownCounter(
            "queue_balance",
            () => new Measurement<long>(-2, [new("pool", "shared")]),
            description: "Current queue balance.");

        const string KeepTag = "keep";

        var builder = WebApplication.CreateBuilder();

        // Listen on any available port
        builder.WebHost.UseUrls("http://0.0.0.0:0");
        builder.WebHost.UseSetting("AllowedHosts", "*");

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource((builder) => builder.AddService("my-service", "my-namespce", "1.2.3"))
            .WithMetrics((builder) =>
            {
                builder.AddAspNetCoreInstrumentation()
                       .AddMeter(meter.Name)
                       .AddPrometheusExporter()
                       .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
                       .AddView(
                           counter.Name,
                           new MetricStreamConfiguration
                           {
                               ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                               TagKeys = [KeepTag],
                           })
                       .AddView(
                           histogram.Name,
                           new ExplicitBucketHistogramConfiguration
                           {
                               Boundaries = [5, 10],
                               ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                               TagKeys = [KeepTag],
                           })
                       .AddView(
                           ignoredHistogram.Name,
                           new Base2ExponentialBucketHistogramConfiguration());
            });

        using var app = builder.Build();

        app.MapGet("ping", () => "pong");
        app.MapPrometheusScrapingEndpoint();

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();

        var baseAddress = addresses!.Addresses
            .Select((p) => new Uri(p))
            .Last();

        // Remap bind-any addresses with loopback address
        baseAddress = new UriBuilder(baseAddress)
        {
            Host = baseAddress.Host switch
            {
                "0.0.0.0" => "127.0.0.1",
                "[::]" => "localhost",
                "::" => "localhost",
                "::0" => "localhost",
                "0:0:0:0:0:0:0:0" => "127.0.0.1",
                _ => baseAddress.Host,
            },
        }.Uri;

        using (var httpClient = new HttpClient())
        {
            _ = await httpClient.GetStringAsync(new Uri(baseAddress, "ping"));
        }

        counter.Add(1, new(KeepTag, "value"), new("filtered", "older"));

        upDownCounter.Add(5, [new("queue", "critical")]);
        upDownCounter.Add(-2, [new("queue", "critical")]);

        histogram.Record(4, new(KeepTag, "value"), new("filtered", "older"));
        histogram.Record(8, new(KeepTag, "value"), new("filtered", "first"));

        ignoredHistogram.Record(42, [new("kind", "exp")]);

        WaitForNextExemplarTimestamp();

        using var activity = new Activity("test");
        activity.Start();

        counter.Add(2, new("keep", "value"), new("filtered", "counter-latest"), new("trace_id", "ignored-trace"), new("span_id", "ignored-span"));
        histogram.Record(9, new("keep", "value"), new("filtered", "histogram-latest"), new("trace_id", "ignored-trace"), new("span_id", "ignored-span"));

        activity.Stop();

        // Act and Assert
        await actAndAssert(baseAddress);
    }

    private static void WaitForNextExemplarTimestamp()
    {
        var timestamp = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow <= timestamp)
        {
            Thread.Sleep(1);
        }
    }
}
