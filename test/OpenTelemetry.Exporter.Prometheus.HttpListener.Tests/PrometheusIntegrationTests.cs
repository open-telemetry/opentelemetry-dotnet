// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Exporter.Prometheus.Tests;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Exporter.Prometheus.HttpListener.Tests;

[Collection(PromToolCollection.Name)]
public class PrometheusIntegrationTests(PromToolFixture fixture, ITestOutputHelper outputHelper)
{
    [EnabledOnDockerPlatformTheory(DockerPlatform.Linux)]
    [InlineData("")]
    [InlineData("text/plain")]
    [InlineData("text/plain;version=0.0.4")]
    [InlineData("text/plain;version=1.0.0", Skip = "https://github.com/open-telemetry/opentelemetry-dotnet/issues/7207")]
    [InlineData("application/openmetrics-text", Skip = "https://github.com/open-telemetry/opentelemetry-dotnet/issues/7207")]
    [InlineData("application/openmetrics-text;version=0.0.4")]
    [InlineData("application/openmetrics-text;version=1.0.0", Skip = "https://github.com/open-telemetry/opentelemetry-dotnet/issues/7207")]
    [InlineData("application/openmetrics-text;version=1.0.0;escaping=allow-utf-8;q=0.5,application/openmetrics-text;version=0.0.1;q=0.4,text/plain;version=1.0.0;escaping=allow-utf-8;q=0.3,text/plain;version=0.0.4;q=0.2,/;q=0.1", Skip = "https://github.com/open-telemetry/opentelemetry-dotnet/issues/7207")]

    public async Task Can_Scrape_Prometheus(string accept)
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

        using var context = PrometheusHttpListenerTests.CreateMeterProvider(
            meter,
#if NET
            configureListener: (options) =>
            {
                int port = TcpPortProvider.GetOpenPort();

                // On Linux we need to explicitly use the internal Docker
                // host address to reach the Prometheus listener from promtool.
                if (OperatingSystem.IsLinux())
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    options.UriPrefixes = [$"http://*:{port}/"];
#pragma warning restore CS0618 // Type or member is obsolete
                }
                else
                {
                    options.Port = port;
                }

                return port;
            },
#endif
            configureMeterProvider: (builder) =>
            {
                builder
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

        // Act
        var actual = await fixture.CheckMetricsAsync(new(context.BaseAddress, "metrics"), accept);

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
