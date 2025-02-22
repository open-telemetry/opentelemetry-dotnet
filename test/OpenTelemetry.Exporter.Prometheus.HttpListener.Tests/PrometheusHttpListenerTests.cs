// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.RegularExpressions;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class PrometheusHttpListenerTests
{
    private const string MeterVersion = "1.0.1";

    private static readonly string MeterName = Utils.GetCurrentMethodName();

    [Theory]
    [InlineData("http://+:9464")]
    [InlineData("http://*:9464")]
    [InlineData("http://+:9464/")]
    [InlineData("http://*:9464/")]
    [InlineData("https://example.com")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://example.com", "https://example.com", "http://127.0.0.1")]
    [InlineData("http://example.com")]
    public void UriPrefixesPositiveTest(params string[] uriPrefixes)
    {
        TestPrometheusHttpListenerUriPrefixOptions(uriPrefixes);
    }

    [Fact]
    public void UriPrefixesNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            TestPrometheusHttpListenerUriPrefixOptions(null!);
        });
    }

    [Fact]
    public void UriPrefixesEmptyList()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            TestPrometheusHttpListenerUriPrefixOptions(new string[] { });
        });
    }

    [Fact]
    public void UriPrefixesInvalid()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            TestPrometheusHttpListenerUriPrefixOptions(new string[] { "ftp://example.com" });
        });
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest();
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoMetrics()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest(skipMetrics: true);
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoOpenMetrics()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: string.Empty);
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_UseOpenMetricsVersionHeader()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: "application/openmetrics-text; version=1.0.0");
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoOpenMetrics_WithMeterTags()
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("meter1", "value1"),
            new("meter2", "value2"),
        };

        await this.RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: string.Empty, meterTags: tags);
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_OpenMetrics_WithMeterTags()
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("meter1", "value1"),
            new("meter2", "value2"),
        };

        await this.RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: "application/openmetrics-text; version=1.0.0", meterTags: tags);
    }

    [Fact]
    public void PrometheusHttpListenerThrowsOnStart()
    {
        Random random = new Random();
        int retryAttempts = 5;
        int port = 0;
        string? address = null;

        PrometheusExporter? exporter = null;
        PrometheusHttpListener? listener = null;

        // Step 1: Start a listener on a random port.
        while (retryAttempts-- != 0)
        {
            port = random.Next(2000, 5000);
            address = $"http://localhost:{port}/";

            try
            {
                exporter = new PrometheusExporter(new());
                listener = new PrometheusHttpListener(
                    exporter,
                    new()
                    {
                        UriPrefixes = new string[] { address },
                    });

                listener.Start();

                break;
            }
            catch
            {
                // ignored
            }
        }

        if (retryAttempts == 0)
        {
            throw new InvalidOperationException("PrometheusHttpListener could not be started");
        }

        // Step 2: Make sure if we start a second listener on the same port an exception is thrown.
        Assert.Throws<HttpListenerException>(() =>
        {
            using var exporter = new PrometheusExporter(new());
            using var listener = new PrometheusHttpListener(
                exporter,
                new()
                {
                    UriPrefixes = new string[] { address! },
                });

            listener.Start();
        });

        exporter?.Dispose();
        listener?.Dispose();
    }

    [Theory]
    [InlineData("application/openmetrics-text")]
    [InlineData("")]
    public async Task PrometheusExporterHttpServerIntegration_TestBufferSizeIncrease_With_LargePayload(string acceptHeader)
    {
        using var meter = new Meter(MeterName, MeterVersion);

        var attributes = new List<KeyValuePair<string, object>>();
        var oneKb = new string('A', 1024);
        for (var x = 0; x < 8500; x++)
        {
            attributes.Add(new KeyValuePair<string, object>(x.ToString(), oneKb));
        }

        using var provider = BuildMeterProvider(meter, attributes, false, out var address);

        for (var x = 0; x < 1000; x++)
        {
            var counter = meter.CreateCounter<double>("counter_double_" + x, unit: "By");
            counter.Add(1);
        }

        using HttpClient client = new HttpClient();

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync($"{address}metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("counter_double_999", content);
        Assert.DoesNotContain('\0', content);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrometheusExporterHttpServerIntegration_Histogram_Exemplars(bool enableExemplars)
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var meter = new Meter(MeterName, MeterVersion);

        using var provider = BuildMeterProvider(meter, [], enableExemplars, out var address);

        var counterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        var counter = meter.CreateCounter<double>("counter_double", unit: "By");
        counter.Add(100.18, counterTags);

        string expectedTraceId;
        string expectedSpanId;
        using (var activity = activitySource.StartActivity("testActivity"))
        {
            Assert.NotNull(activity);

            counter.Add(0.99, counterTags);
            expectedTraceId = activity.TraceId.ToHexString();
            expectedSpanId = activity.SpanId.ToHexString();
        }

        using HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Add("Accept", "application/openmetrics-text");

        using var response = await client.GetAsync($"{address}metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        var expected =
            enableExemplars
                ? $$"""
                    \# TYPE target info
                    \# HELP target Target metadata
                    target_info\{service_name="my_service",service_instance_id="id1"} 1
                    \# TYPE otel_scope_info info
                    \# HELP otel_scope_info Scope metadata
                    otel_scope_info\{otel_scope_name="{{MeterName}}"} 1
                    \# TYPE counter_double_bytes counter
                    \# UNIT counter_double_bytes bytes
                    counter_double_bytes_total\{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",key1="value1",key2="value2"} 101\.17 \d+.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 0\.99 \d+.\d{3}
                    \# EOF
                    """.Replace("\r\n", "\n")
                : $$"""
                    \# TYPE target info
                    \# HELP target Target metadata
                    target_info\{service_name="my_service",service_instance_id="id1"} 1
                    \# TYPE otel_scope_info info
                    \# HELP otel_scope_info Scope metadata
                    otel_scope_info\{otel_scope_name="{{MeterName}}"} 1
                    \# TYPE counter_double_bytes counter
                    \# UNIT counter_double_bytes bytes
                    counter_double_bytes_total\{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",key1="value1",key2="value2"} 101\.17 \d+.\d{3}
                    \# EOF
                    """.Replace("\r\n", "\n");

        var match = Regex.Match(content, expected);
        Assert.True(match.Success);

        if (enableExemplars)
        {
            Assert.Equal(expectedTraceId, match.Groups[1].Value);
            Assert.Equal(expectedSpanId, match.Groups[2].Value);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PrometheusExporterHttpServerIntegration_Exemplars_OpenMetricsFormat(bool openMetricsFormat)
    {
        using var activitySource = new ActivitySource(Utils.GetCurrentMethodName());
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySource.Name)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var meter = new Meter(MeterName, MeterVersion);

        using var provider = BuildMeterProvider(meter, [], true, out var address);

        var counterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        var counter = meter.CreateCounter<double>("counter_double", unit: "By");
        counter.Add(100.18, counterTags);

        string expectedTraceId;
        string expectedSpanId;
        using (var activity = activitySource.StartActivity("testActivity"))
        {
            Assert.NotNull(activity);

            counter.Add(0.99, counterTags);
            expectedTraceId = activity.TraceId.ToHexString();
            expectedSpanId = activity.SpanId.ToHexString();
        }

        using HttpClient client = new HttpClient();
        if (openMetricsFormat)
        {
            client.DefaultRequestHeaders.Add("Accept", "application/openmetrics-text");
        }

        using var response = await client.GetAsync($"{address}metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        var expected =
            openMetricsFormat
                ? $$"""
                    \# TYPE target info
                    \# HELP target Target metadata
                    target_info\{service_name="my_service",service_instance_id="id1"} 1
                    \# TYPE otel_scope_info info
                    \# HELP otel_scope_info Scope metadata
                    otel_scope_info\{otel_scope_name="{{MeterName}}"} 1
                    \# TYPE counter_double_bytes counter
                    \# UNIT counter_double_bytes bytes
                    counter_double_bytes_total\{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",key1="value1",key2="value2"} 101\.17 \d+.\d{3} \# \{trace_id="([a-z0-9]{32})",span_id="([a-z0-9]{16})"} 0\.99 \d+.\d{3}
                    \# EOF
                    """.Replace("\r\n", "\n")
                : $$"""
                    \# TYPE counter_double_bytes_total counter
                    \# UNIT counter_double_bytes_total bytes
                    counter_double_bytes_total\{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",key1="value1",key2="value2"} 101\.17 \d+
                    \# EOF
                    """.Replace("\r\n", "\n");

        var match = Regex.Match(content, expected);
        Assert.True(match.Success);

        if (openMetricsFormat)
        {
            Assert.Equal(expectedTraceId, match.Groups[1].Value);
            Assert.Equal(expectedSpanId, match.Groups[2].Value);
        }
    }

    private static void TestPrometheusHttpListenerUriPrefixOptions(string[] uriPrefixes)
    {
        using var exporter = new PrometheusExporter(new());
        using var listener = new PrometheusHttpListener(
            exporter,
            new()
            {
                UriPrefixes = uriPrefixes,
            });
    }

    private static MeterProvider BuildMeterProvider(Meter meter, IEnumerable<KeyValuePair<string, object>> attributes, bool enableExemplars, out string address)
    {
        Random random = new Random();
        int retryAttempts = 5;
        int port = 0;
        string? generatedAddress = null;
        MeterProvider? provider = null;

        while (retryAttempts-- != 0)
        {
            port = random.Next(2000, 5000);
            generatedAddress = $"http://localhost:{port}/";

            try
            {
                var builder = Sdk.CreateMeterProviderBuilder()
                    .AddMeter(meter.Name)
                    .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1").AddAttributes(attributes))
                    .AddPrometheusHttpListener(options =>
                    {
                        options.UriPrefixes = new string[] { generatedAddress };
                    });

                if (enableExemplars)
                {
                    builder.SetExemplarFilter(ExemplarFilterType.AlwaysOn);
                }

                provider = builder.Build();

                break;
            }
            catch
            {
                // ignored
            }
        }

        address = generatedAddress!;

        if (provider == null)
        {
            throw new InvalidOperationException("HttpListener could not be started");
        }

        return provider;
    }

    private async Task RunPrometheusExporterHttpServerIntegrationTest(
        bool skipMetrics = false,
        string acceptHeader = "application/openmetrics-text",
        KeyValuePair<string, object?>[]? meterTags = null,
        bool enableExemplars = false)
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text");

        using var meter = new Meter(MeterName, MeterVersion, meterTags);

        using var provider = BuildMeterProvider(meter, [], enableExemplars, out var address);

        var counterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        var counter = meter.CreateCounter<double>("counter_double", unit: "By");
        if (!skipMetrics)
        {
            counter.Add(100.18D, counterTags);
            counter.Add(0.99D, counterTags);
        }

        using HttpClient client = new HttpClient();

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync($"{address}metrics");

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

        var content = await response.Content.ReadAsStringAsync();

        if (!skipMetrics)
        {
            var additionalTags = meterTags != null && meterTags.Any()
                ? $"{string.Join(",", meterTags.Select(x => $"{x.Key}=\"{x.Value}\""))},"
                : string.Empty;

            var expected = requestOpenMetrics
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

                    """.Replace("\r\n", "\n")
                : $$"""
                    # TYPE counter_double_bytes_total counter
                    # UNIT counter_double_bytes_total bytes
                    counter_double_bytes_total{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",{{additionalTags}}key1="value1",key2="value2"} 101.17 (\d+)
                    # EOF

                    """.Replace("\r\n", "\n");

            Assert.Matches("^" + expected + "$", content);
        }
    }
}
