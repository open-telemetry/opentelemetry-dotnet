// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.AspNetCore.Tests;

public sealed class PrometheusExporterMiddlewareTests
{
    private const string MeterVersion = "1.0.1";

    private static readonly string MeterName = Utils.GetCurrentMethodName();

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint());
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_DoubleHistogram()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            enableExemplars: true);

        using var meter = new Meter(MeterName, MeterVersion);

        // Due to the default histogram buckets, exemplars will only be recorded
        // for some of the values (the last recorded value for each buckets wins):
        //                          x   x   x   x    -    -    x    -    x    x
        var values = new double[] { 10, 20, 50, 100, 150, 200, 250, 300, 350, 10001 };

        var histogram = meter.CreateHistogram<double>("dbl_histogram", unit: "s");
        foreach (var value in values)
        {
            using var activity = activitySource.StartActivity("testActivity");
            histogram.Record(value);
        }

        using var client = host.GetTestClient();

        using var response = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
            Headers =
            {
                { "Accept", "application/openmetrics-text" },
            },
        });
        var text = await response.Content.ReadAsStringAsync();

        var expectedPattern =
            """
            \# TYPE target info
            \# HELP target Target metadata
            target_info\{service_name="my_service",service_instance_id="id1"} 1
            \# TYPE otel_scope_info info
            \# HELP otel_scope_info Scope metadata
            otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
            \# TYPE dbl_histogram_seconds histogram
            \# UNIT dbl_histogram_seconds seconds
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="0"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10"} 1 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 10 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="25"} 2 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 20 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="50"} 3 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 50 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="75"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="100"} 4 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 100 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="250"} 7 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 250 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="500"} 9 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 350 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="750"} 9 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="1000"} 9 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="2500"} 9 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5000"} 9 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="7500"} 9 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10000"} 9 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="\+Inf"} 10 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 10001 \d+\.\d{3}
            dbl_histogram_seconds_sum\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 11431 \d+\.\d{3}
            dbl_histogram_seconds_count\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 10 \d+\.\d{3}
            \# EOF
            """.ReplaceLineEndings("\n");

        Assert.Matches(expectedPattern, text);

        await host.StopAsync();
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_DoubleHistogram_ValuesAreCorrect()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            enableExemplars: true);

        var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        using var meter = new Meter(MeterName, MeterVersion);

        var histogram = meter.CreateHistogram<double>("dbl_histogram", unit: "s");

        using (activitySource.StartActivity("testActivity"))
        {
            // Falls within <= 100 bucket
            histogram.Record(90.0);
        }

        using (activitySource.StartActivity("testActivity"))
        {
            // Falls within <= 100 bucket
            // More than 90.0, so supersedes existing exemplar

            histogram.Record(95.1);
        }

        string expectedTraceId;
        string expectedSpanId;
        using (var activity = activitySource.StartActivity("testActivity"))
        {
            Assert.NotNull(activity);

            // Falls within <= 100 bucket
            // Less than 95.1, but still supersedes the existing exemplar

            histogram.Record(80.2);

            expectedTraceId = activity.TraceId.ToHexString();
            expectedSpanId = activity.SpanId.ToHexString();
        }

        using var client = host.GetTestClient();

        using var response = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
            Headers =
            {
                { "Accept", "application/openmetrics-text" },
            },
        });
        var text = await response.Content.ReadAsStringAsync();

        var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var expectedPattern =
            """
            \# TYPE target info
            \# HELP target Target metadata
            target_info\{service_name="my_service",service_instance_id="id1"} 1
            \# TYPE otel_scope_info info
            \# HELP otel_scope_info Scope metadata
            otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
            \# TYPE dbl_histogram_seconds histogram
            \# UNIT dbl_histogram_seconds seconds
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="0"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="25"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="50"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="75"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="100"} 3 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 80.2 (\d+\.\d{3})
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="250"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="500"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="750"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="1000"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="2500"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5000"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="7500"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10000"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="\+Inf"} 3 \d+\.\d{3}
            dbl_histogram_seconds_sum\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 265.3 \d+\.\d{3}
            dbl_histogram_seconds_count\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 3 \d+\.\d{3}
            \# EOF
            """.ReplaceLineEndings("\n");

        Assert.Matches(expectedPattern, text);

        var match = Regex.Match(text, expectedPattern);
        Assert.True(match.Success);

        var traceId = match.Groups[1].Value;
        Assert.Equal(expectedTraceId, traceId);

        var spanId = match.Groups[2].Value;
        Assert.Equal(expectedSpanId, spanId);

        var timestamp = double.Parse(match.Groups[3].Value);
        Assert.True(timestamp >= beginTimestamp / 1000.0 && timestamp <= endTimestamp / 1000.0);

        await host.StopAsync();
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_PersistBetweenScrapes()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            configureOptions: options =>
            {
                // Disable caching to test exemplars persisting between scrapes
                options.ScrapeResponseCacheDurationMilliseconds = 0;
            },
            enableExemplars: true);

        using var meter = new Meter(MeterName, MeterVersion);

        var histogram = meter.CreateHistogram<double>("dbl_histogram", unit: "s");

        using (activitySource.StartActivity("testActivity"))
        {
            // Falls within <= 100 bucket
            histogram.Record(90.0);
        }

        using var client = host.GetTestClient();

        // First response is discarded
        var text1 = await client.GetStringAsync("/metrics");

        using var response2 = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
            Headers =
            {
                { "Accept", "application/openmetrics-text" },
            },
        });
        var text2 = await response2.Content.ReadAsStringAsync();

        var expectedPattern =
            """
            \# TYPE target info
            \# HELP target Target metadata
            target_info\{service_name="my_service",service_instance_id="id1"} 1
            \# TYPE otel_scope_info info
            \# HELP otel_scope_info Scope metadata
            otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
            \# TYPE dbl_histogram_seconds histogram
            \# UNIT dbl_histogram_seconds seconds
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="0"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="25"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="50"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="75"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="100"} 1 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 90 (\d+\.\d{3})
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="250"} 1 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="500"} 1 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="750"} 1 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="1000"} 1 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="2500"} 1 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5000"} 1 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="7500"} 1 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10000"} 1 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="\+Inf"} 1 \d+\.\d{3}
            dbl_histogram_seconds_sum\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 90 \d+\.\d{3}
            dbl_histogram_seconds_count\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 1 \d+\.\d{3}
            \# EOF
            """.ReplaceLineEndings("\n");

        Assert.Matches(expectedPattern, text2);

        await host.StopAsync();
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_LongHistogram_ValuesAreCorrect()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            enableExemplars: true);

        var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        using var meter = new Meter(MeterName, MeterVersion);

        var histogram = meter.CreateHistogram<long>("histogram", unit: "s");

        using (activitySource.StartActivity("testActivity"))
        {
            histogram.Record(90L);
        }

        using (activitySource.StartActivity("testActivity"))
        {
            histogram.Record(95L);
        }

        string expectedTraceId;
        string expectedSpanId;
        using (var activity = activitySource.StartActivity("testActivity"))
        {
            Assert.NotNull(activity);
            histogram.Record(80L);

            expectedTraceId = activity.TraceId.ToHexString();
            expectedSpanId = activity.SpanId.ToHexString();
        }

        using var client = host.GetTestClient();

        using var response = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
            Headers =
            {
                { "Accept", "application/openmetrics-text" },
            },
        });
        var text = await response.Content.ReadAsStringAsync();

        var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var expectedPattern =
            """
            \# TYPE target info
            \# HELP target Target metadata
            target_info\{service_name="my_service",service_instance_id="id1"} 1
            \# TYPE otel_scope_info info
            \# HELP otel_scope_info Scope metadata
            otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
            \# TYPE histogram_seconds histogram
            \# UNIT histogram_seconds seconds
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="0"} 0 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5"} 0 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10"} 0 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="25"} 0 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="50"} 0 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="75"} 0 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="100"} 3 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 80 (\d+\.\d{3})
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="250"} 3 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="500"} 3 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="750"} 3 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="1000"} 3 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="2500"} 3 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5000"} 3 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="7500"} 3 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10000"} 3 \d+\.\d{3}
            histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="\+Inf"} 3 \d+\.\d{3}
            histogram_seconds_sum\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 265 \d+\.\d{3}
            histogram_seconds_count\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 3 \d+\.\d{3}
            \# EOF
            """.ReplaceLineEndings("\n");

        Assert.Matches(expectedPattern, text);

        var match = Regex.Match(text, expectedPattern);
        Assert.True(match.Success);

        var traceId = match.Groups[1].Value;
        Assert.Equal(expectedTraceId, traceId);

        var spanId = match.Groups[2].Value;
        Assert.Equal(expectedSpanId, spanId);

        var timestamp = double.Parse(match.Groups[3].Value);
        Assert.True(timestamp >= beginTimestamp / 1000.0 && timestamp <= endTimestamp / 1000.0);

        await host.StopAsync();
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_DoubleHistogram_Tags()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            enableExemplars: true,
            enableTagFiltering: true);

        using var meter = new Meter(MeterName, MeterVersion);

        var histogram = meter.CreateHistogram<double>("dbl_histogram", unit: "s");
        using (activitySource.StartActivity("testActivity"))
        {
            // This is precisely 128 chars of labels and values
            histogram.Record(10, new KeyValuePair<string, object?>("ab", "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do"));
        }

        using (activitySource.StartActivity("testActivity"))
        {
            // This is precisely 129 chars of labels and values
            histogram.Record(20, new KeyValuePair<string, object?>("abc", "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do"));
        }

        using (activitySource.StartActivity("testActivity"))
        {
            // This is precisely 127 chars of labels and values
            histogram.Record(50, new KeyValuePair<string, object?>("a", "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do"));
        }

        using (activitySource.StartActivity("testActivity"))
        {
            histogram.Record(80, new KeyValuePair<string, object?>("key1", "value1"), new KeyValuePair<string, object?>("key2", "value2"));
        }

        using (activitySource.StartActivity("testActivity"))
        {
            // This is precisely 129 chars of labels and values
            histogram.Record(
                200,
                new KeyValuePair<string, object?>("key1", "value1"),
                new KeyValuePair<string, object?>("a", "Lorem ipsum dolor sit amet, consectetur adipiscing elit"));
        }

        using var client = host.GetTestClient();

        using var response = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
            Headers =
            {
                { "Accept", "application/openmetrics-text" },
            },
        });
        var text = await response.Content.ReadAsStringAsync();

        var expectedPattern =
            """
            \# TYPE target info
            \# HELP target Target metadata
            target_info\{service_name="my_service",service_instance_id="id1"} 1
            \# TYPE otel_scope_info info
            \# HELP otel_scope_info Scope metadata
            otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
            \# TYPE dbl_histogram_seconds histogram
            \# UNIT dbl_histogram_seconds seconds
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="0"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5"} 0 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10"} 1 \d+\.\d{3} \# \{trace_id="[a-z0-9]{32}",span_id="[a-z0-9]{16}",ab="Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do"} 10 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="25"} 2 \d+\.\d{3} \# \{trace_id="[a-z0-9]{32}",span_id="[a-z0-9]{16}"} 20 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="50"} 3 \d+\.\d{3} \# \{trace_id="[a-z0-9]{32}",span_id="[a-z0-9]{16}",a="Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do"} 50 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="75"} 3 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="100"} 4 \d+\.\d{3} \# \{trace_id="[a-z0-9]{32}",span_id="[a-z0-9]{16}",key1="value1",key2="value2"} 80 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="250"} 5 \d+\.\d{3} \# \{trace_id="[a-z0-9]{32}",span_id="[a-z0-9]{16}"} 200 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="500"} 5 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="750"} 5 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="1000"} 5 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="2500"} 5 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="5000"} 5 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="7500"} 5 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="10000"} 5 \d+\.\d{3}
            dbl_histogram_seconds_bucket\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",le="\+Inf"} 5 \d+\.\d{3}
            dbl_histogram_seconds_sum\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 360 \d+\.\d{3}
            dbl_histogram_seconds_count\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 5 \d+\.\d{3}
            \# EOF
            """.ReplaceLineEndings("\n");

        Assert.Matches(expectedPattern, text);

        await host.StopAsync();
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_DoubleCounter()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            enableExemplars: true);

        using var meter = new Meter(MeterName, MeterVersion);

        var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var counter = meter.CreateCounter<double>("counter", unit: "s");
        using (activitySource.StartActivity("testActivity"))
        {
            counter.Add(123.456);
        }

        string expectedTraceId;
        string expectedSpanId;
        using (var activity = activitySource.StartActivity("testActivity"))
        {
            Assert.NotNull(activity);

            counter.Add(78.9);
            expectedTraceId = activity.TraceId.ToHexString();
            expectedSpanId = activity.SpanId.ToHexString();
        }

        using var client = host.GetTestClient();

        using var response = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
            Headers =
            {
                { "Accept", "application/openmetrics-text" },
            },
        });

        var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var text = await response.Content.ReadAsStringAsync();

        var expectedPattern =
            """
            \# TYPE target info
            \# HELP target Target metadata
            target_info\{service_name="my_service",service_instance_id="id1"} 1
            \# TYPE otel_scope_info info
            \# HELP otel_scope_info Scope metadata
            otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
            \# TYPE counter_seconds counter
            \# UNIT counter_seconds seconds
            counter_seconds_total\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 202\.356 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 78\.9 (\d+\.\d{3})
            \# EOF
            """.ReplaceLineEndings("\n");

        var match = Regex.Match(text, expectedPattern);
        Assert.True(match.Success);

        Assert.Equal(expectedTraceId, match.Groups[1].Value);
        Assert.Equal(expectedSpanId, match.Groups[2].Value);

        var timestamp = double.Parse(match.Groups[3].Value);
        Assert.True(timestamp >= beginTimestamp / 1000.0 && timestamp <= endTimestamp / 1000.0);

        await host.StopAsync();
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_LongCounter()
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            enableExemplars: true);

        using var meter = new Meter(MeterName, MeterVersion);

        var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var counter = meter.CreateCounter<long>("counter", unit: "s");
        using (activitySource.StartActivity("testActivity"))
        {
            counter.Add(123L);
        }

        string expectedTraceId;
        string expectedSpanId;
        using (var activity = activitySource.StartActivity("testActivity"))
        {
            Assert.NotNull(activity);

            counter.Add(78L);
            expectedTraceId = activity.TraceId.ToHexString();
            expectedSpanId = activity.SpanId.ToHexString();
        }

        using var client = host.GetTestClient();

        using var response = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
            Headers =
            {
                { "Accept", "application/openmetrics-text" },
            },
        });

        var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var text = await response.Content.ReadAsStringAsync();

        var expectedPattern =
            """
            \# TYPE target info
            \# HELP target Target metadata
            target_info\{service_name="my_service",service_instance_id="id1"} 1
            \# TYPE otel_scope_info info
            \# HELP otel_scope_info Scope metadata
            otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
            \# TYPE counter_seconds counter
            \# UNIT counter_seconds seconds
            counter_seconds_total\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 201 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 78 (\d+\.\d{3})
            \# EOF
            """.ReplaceLineEndings("\n");

        var match = Regex.Match(text, expectedPattern);
        Assert.True(match.Success);

        Assert.Equal(expectedTraceId, match.Groups[1].Value);
        Assert.Equal(expectedSpanId, match.Groups[2].Value);

        var timestamp = double.Parse(match.Groups[3].Value);
        Assert.True(timestamp >= beginTimestamp / 1000.0 && timestamp <= endTimestamp / 1000.0);

        await host.StopAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_LongCounter_Tags(bool enableTagFiltering)
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            enableExemplars: true,
            enableTagFiltering: enableTagFiltering);

        using var meter = new Meter(MeterName, MeterVersion);

        var counter = meter.CreateCounter<long>("counter", unit: "s");
        using (activitySource.StartActivity("testActivity"))
        {
            counter.Add(123L, new KeyValuePair<string, object?>("key1", "value1"));
        }

        using var client = host.GetTestClient();

        using var response = await client.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
            Headers =
            {
                { "Accept", "application/openmetrics-text" },
            },
        });

        var text = await response.Content.ReadAsStringAsync();

        var expectedPattern =
            enableTagFiltering
                ? """
                  \# TYPE target info
                  \# HELP target Target metadata
                  target_info\{service_name="my_service",service_instance_id="id1"} 1
                  \# TYPE otel_scope_info info
                  \# HELP otel_scope_info Scope metadata
                  otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
                  \# TYPE counter_seconds counter
                  \# UNIT counter_seconds seconds
                  counter_seconds_total\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 123 \d+\.\d{3} \# \{trace_id="[a-z0-9]{32}",span_id="[a-z0-9]{16}",key1="value1"} 123 \d+\.\d{3}
                  \# EOF
                  """.ReplaceLineEndings("\n")
                : """
                  \# TYPE target info
                  \# HELP target Target metadata
                  target_info\{service_name="my_service",service_instance_id="id1"} 1
                  \# TYPE otel_scope_info info
                  \# HELP otel_scope_info Scope metadata
                  otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
                  \# TYPE counter_seconds counter
                  \# UNIT counter_seconds seconds
                  counter_seconds_total\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1",key1="value1"} 123 \d+\.\d{3} \# \{trace_id="[a-z0-9]{32}",span_id="[a-z0-9]{16}"} 123 \d+\.\d{3}
                  \# EOF
                  """.ReplaceLineEndings("\n");

        Assert.Matches(expectedPattern, text);

        await host.StopAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrometheusExporterMiddlewareIntegration_Exemplars_OpenMetricsFormat(bool openMetricsFormat)
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            enableExemplars: true);

        using var meter = new Meter(MeterName, MeterVersion);

        var counter = meter.CreateCounter<long>("counter", unit: "s");
        using (activitySource.StartActivity("testActivity"))
        {
            counter.Add(123L);
        }

        using (activitySource.StartActivity("testActivity"))
        {
            counter.Add(78L);
        }

        using var client = host.GetTestClient();

        var request = new HttpRequestMessage
        {
            RequestUri = new Uri("/metrics", UriKind.Relative),
        };
        if (openMetricsFormat)
        {
            request.Headers.Add("Accept", "application/openmetrics-text");
        }

        using var response = await client.SendAsync(request);

        var text = await response.Content.ReadAsStringAsync();

        var expectedPattern =
            openMetricsFormat
                ? """
                  \# TYPE target info
                  \# HELP target Target metadata
                  target_info\{service_name="my_service",service_instance_id="id1"} 1
                  \# TYPE otel_scope_info info
                  \# HELP otel_scope_info Scope metadata
                  otel_scope_info\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor"} 1
                  \# TYPE counter_seconds counter
                  \# UNIT counter_seconds seconds
                  counter_seconds_total\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 201 \d+\.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 78 (\d+\.\d{3})
                  \# EOF
                  """.ReplaceLineEndings("\n")
                : """
                  \# TYPE counter_seconds_total counter
                  \# UNIT counter_seconds_total seconds
                  counter_seconds_total\{otel_scope_name="OpenTelemetry\.Exporter\.Prometheus\.AspNetCore\.Tests\.PrometheusExporterMiddlewareTests\.\.cctor",otel_scope_version="1\.0\.1"} 201 \d+
                  \# EOF
                  """.ReplaceLineEndings("\n");

        var match = Regex.Match(text, expectedPattern);
        Assert.True(match.Success);

        await host.StopAsync();
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_Options()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_options",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = "metrics_options"));
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_OptionsFallback()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = null));
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_OptionsViaAddPrometheusExporter()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_from_AddPrometheusExporter",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            configureOptions: o => o.ScrapeEndpointPath = "/metrics_from_AddPrometheusExporter");
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_PathOverride()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_override",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics_override"));
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_WithPathNamedOptionsOverride()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_override",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                meterProvider: null,
                predicate: null,
                path: null,
                configureBranchedPipeline: null,
                optionsName: "myOptions"),
            services =>
            {
                services.Configure<PrometheusAspNetCoreOptions>("myOptions", o => o.ScrapeEndpointPath = "/metrics_override");
            });
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_Predicate()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_predicate?enabled=true",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(httpcontext => httpcontext.Request.Path == "/metrics_predicate" && httpcontext.Request.Query["enabled"] == "true"));
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MixedPredicateAndPath()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_predicate",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                meterProvider: null,
                predicate: httpcontext => httpcontext.Request.Path == "/metrics_predicate",
                path: "/metrics_path",
                configureBranchedPipeline: branch => branch.Use((context, next) =>
                {
                    context.Response.Headers.Append("X-MiddlewareExecuted", "true");
                    return next();
                }),
                optionsName: null),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = "/metrics_options"),
            validateResponse: rsp =>
            {
                if (!rsp.Headers.TryGetValues("X-MiddlewareExecuted", out IEnumerable<string>? headers))
                {
                    headers = Array.Empty<string>();
                }

                Assert.Equal("true", headers.FirstOrDefault());
            });
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MixedPath()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_path",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                meterProvider: null,
                predicate: null,
                path: "/metrics_path",
                configureBranchedPipeline: branch => branch.Use((context, next) =>
                {
                    context.Response.Headers.Append("X-MiddlewareExecuted", "true");
                    return next();
                }),
                optionsName: null),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = "/metrics_options"),
            validateResponse: rsp =>
            {
                if (!rsp.Headers.TryGetValues("X-MiddlewareExecuted", out IEnumerable<string>? headers))
                {
                    headers = Array.Empty<string>();
                }

                Assert.Equal("true", headers.FirstOrDefault());
            });
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_MeterProvider()
    {
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MeterName)
            .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1"))
            .AddPrometheusExporter()
            .Build();

        await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                meterProvider: meterProvider,
                predicate: null,
                path: null,
                configureBranchedPipeline: null,
                optionsName: null),
            registerMeterProvider: false);
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_NoMetrics()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            skipMetrics: true);
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MapEndpoint()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint()),
            services => services.AddRouting());
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithPathOverride()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_path",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint("metrics_path")),
            services => services.AddRouting());
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithPathNamedOptionsOverride()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_path",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint(
                path: null,
                meterProvider: null,
                configureBranchedPipeline: null,
                optionsName: "myOptions")),
            services =>
            {
                services.AddRouting();
                services.Configure<PrometheusAspNetCoreOptions>("myOptions", o => o.ScrapeEndpointPath = "/metrics_path");
            });
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithMeterProvider()
    {
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MeterName)
            .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1"))
            .AddPrometheusExporter()
            .Build();

        await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint(
                path: null,
                meterProvider: meterProvider,
                configureBranchedPipeline: null,
                optionsName: null)),
            services => services.AddRouting(),
            registerMeterProvider: false);
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_TextPlainResponse()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "text/plain");
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_UseOpenMetricsVersionHeader()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "application/openmetrics-text; version=1.0.0");
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_TextPlainResponse_WithMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "text/plain",
            meterTags: meterTags);
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_UseOpenMetricsVersionHeader_WithMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "application/openmetrics-text; version=1.0.0",
            meterTags: meterTags);
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_CanServeOpenMetricsAndPlainFormats_NoMeterTags()
    {
        await RunPrometheusExporterMiddlewareIntegrationTestWithBothFormats();
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_CanServeOpenMetricsAndPlainFormats_WithMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        await RunPrometheusExporterMiddlewareIntegrationTestWithBothFormats(meterTags);
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_TestBufferSizeIncrease_With_LotOfMetrics()
    {
        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint());

        using var meter = new Meter(MeterName, MeterVersion);

        for (var x = 0; x < 1000; x++)
        {
            var counter = meter.CreateCounter<double>("counter_double_" + x, unit: "By");
            counter.Add(1);
        }

        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/metrics");
        var text = await response.Content.ReadAsStringAsync();

        Assert.NotEmpty(text);

        await host.StopAsync();
    }

    private static async Task RunPrometheusExporterMiddlewareIntegrationTestWithBothFormats(KeyValuePair<string, object?>[]? meterTags = null)
    {
        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint());

        var counterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        using var meter = new Meter(MeterName, MeterVersion, meterTags);

        var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var counter = meter.CreateCounter<double>("counter_double", unit: "By");
        counter.Add(100.18D, counterTags);
        counter.Add(0.99D, counterTags);

        var testCases = new bool[] { true, false, true, true, false };

        using var client = host.GetTestClient();

        foreach (var testCase in testCases)
        {
            using var request = new HttpRequestMessage
            {
                Headers = { { "Accept", testCase ? "application/openmetrics-text" : "text/plain" } },
                RequestUri = new Uri("/metrics", UriKind.Relative),
                Method = HttpMethod.Get,
            };
            using var response = await client.SendAsync(request);
            var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            await VerifyAsync(beginTimestamp, endTimestamp, response, testCase, meterTags);
        }

        await host.StopAsync();
    }

    private static async Task RunPrometheusExporterMiddlewareIntegrationTest(
        string path,
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection>? configureServices = null,
        Action<HttpResponseMessage>? validateResponse = null,
        bool registerMeterProvider = true,
        Action<PrometheusAspNetCoreOptions>? configureOptions = null,
        bool skipMetrics = false,
        bool enableExemplars = false,
        string acceptHeader = "application/openmetrics-text",
        KeyValuePair<string, object?>[]? meterTags = null)
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text");

        using var host = await StartTestHostAsync(configure, configureServices, registerMeterProvider, false, enableExemplars, configureOptions);

        var counterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        using var meter = new Meter(MeterName, MeterVersion, meterTags);

        var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var counter = meter.CreateCounter<double>("counter_double", unit: "By");
        if (!skipMetrics)
        {
            counter.Add(100.18D, counterTags);
            counter.Add(0.99D, counterTags);
        }

        using var client = host.GetTestClient();

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync(path);

        var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        if (!skipMetrics)
        {
            await VerifyAsync(beginTimestamp, endTimestamp, response, requestOpenMetrics, meterTags);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        validateResponse?.Invoke(response);

        await host.StopAsync();
    }

    private static async Task VerifyAsync(long beginTimestamp, long endTimestamp, HttpResponseMessage response, bool requestOpenMetrics, KeyValuePair<string, object?>[]? meterTags)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.Contains("Last-Modified"));

        if (requestOpenMetrics)
        {
            Assert.Equal("application/openmetrics-text; version=1.0.0; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        }
        else
        {
            Assert.Equal("text/plain; charset=utf-8; version=0.0.4", response.Content.Headers.ContentType!.ToString());
        }

        var additionalTags = meterTags != null && meterTags.Any()
            ? $"{string.Join(",", meterTags.Select(x => $"{x.Key}=\"{x.Value}\""))},"
            : string.Empty;

        string content = await response.Content.ReadAsStringAsync();

        string expected = requestOpenMetrics
            ? $$"""
                    # TYPE target info
                    # HELP target Target metadata
                    target_info{service_name="my_service",service_instance_id="id1"} 1
                    # TYPE otel_scope_info info
                    # HELP otel_scope_info Scope metadata
                    otel_scope_info{otel_scope_name="{{MeterName}}"} 1
                    # TYPE counter_double_bytes counter
                    # UNIT counter_double_bytes bytes
                    counter_double_bytes_total{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",{{additionalTags}}key1="value1",key2="value2"} 101.17 (\d+\.\d{3})
                    # EOF

                    """.ReplaceLineEndings("\n")
            : $$"""
                    # TYPE counter_double_bytes_total counter
                    # UNIT counter_double_bytes_total bytes
                    counter_double_bytes_total{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",{{additionalTags}}key1="value1",key2="value2"} 101.17 (\d+)
                    # EOF

                    """.ReplaceLineEndings("\n");

        var matches = Regex.Matches(content, "^" + expected + "$");

        Assert.True(matches.Count == 1, content);

        var timestamp = long.Parse(matches[0].Groups[1].Value.Replace(".", string.Empty));

        Assert.True(beginTimestamp <= timestamp && timestamp <= endTimestamp, $"{beginTimestamp} {timestamp} {endTimestamp}");
    }

    private static Task<IHost> StartTestHostAsync(
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection>? configureServices = null,
        bool registerMeterProvider = true,
        bool enableExemplars = false,
        bool enableTagFiltering = false,
        Action<PrometheusAspNetCoreOptions>? configureOptions = null)
    {
        return new HostBuilder()
            .ConfigureWebHost(webBuilder => webBuilder
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    if (registerMeterProvider)
                    {
                        services.AddOpenTelemetry().WithMetrics(builder =>
                        {
                            builder
                                .AddView(i => enableTagFiltering
                                    ? new MetricStreamConfiguration { TagKeys = [], }
                                    : null)
                                .ConfigureResource(x => x.Clear()
                                    .AddService("my_service", serviceInstanceId: "id1"))
                                .AddMeter(MeterName)
                                .AddPrometheusExporter(o =>
                                {
                                    configureOptions?.Invoke(o);
                                });

                            if (enableExemplars)
                            {
                                builder.SetExemplarFilter(ExemplarFilterType.AlwaysOn);
                            }
                        });
                    }

                    configureServices?.Invoke(services);
                })
                .Configure(configure))
            .StartAsync();
    }
}
#endif
