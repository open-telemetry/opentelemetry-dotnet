// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
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
            TestPrometheusHttpListenerUriPrefixOptions([]);
        });
    }

    [Fact]
    public void UriPrefixesInvalid()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            TestPrometheusHttpListenerUriPrefixOptions(["ftp://example.com"]);
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
                        UriPrefixes = [address],
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
                    UriPrefixes = [address!],
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
            attributes.Add(new KeyValuePair<string, object>(x.ToString(CultureInfo.InvariantCulture), oneKb));
        }

        var provider = BuildMeterProvider(meter, attributes, out var address);

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
        Assert.Contains("counter_double_999", content, StringComparison.Ordinal);
        Assert.DoesNotContain('\0', content);

        provider.Dispose();
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

    private static MeterProvider BuildMeterProvider(Meter meter, IEnumerable<KeyValuePair<string, object>> attributes, out string address)
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
                provider = Sdk.CreateMeterProviderBuilder()
                    .AddMeter(meter.Name)
                    .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1").AddAttributes(attributes))
                    .AddPrometheusHttpListener(options =>
                    {
                        options.UriPrefixes = [generatedAddress];
                    })
                    .Build();

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

    private static Task RunPrometheusExporterHttpServerIntegrationTest(bool skipMetrics = false, string acceptHeader = "application/openmetrics-text", KeyValuePair<string, object?>[]? meterTags = null)
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text", StringComparison.Ordinal);

        using var meter = new Meter(MeterName, MeterVersion, meterTags);

        var provider = BuildMeterProvider(meter, [], out var address);

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

        if (!skipMetrics)
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
                ? $"{string.Join(",", meterTags.Select(x => $"{x.Key}='{x.Value}'"))},"
                : string.Empty;

            var content = await response.Content.ReadAsStringAsync();

            var expected = requestOpenMetrics
                ? "# TYPE target info\n"
                  + "# HELP target Target metadata\n"
                  + "target_info{service_name='my_service',service_instance_id='id1'} 1\n"
                  + "# TYPE otel_scope_info info\n"
                  + "# HELP otel_scope_info Scope metadata\n"
                  + $"otel_scope_info{{otel_scope_name='{MeterName}'}} 1\n"
                  + "# TYPE counter_double_bytes counter\n"
                  + "# UNIT counter_double_bytes bytes\n"
                  + $"counter_double_bytes_total{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',{additionalTags}key1='value1',key2='value2'}} 101.17 (\\d+\\.\\d{{3}})\n"
                  + "# EOF\n"
                : "# TYPE counter_double_bytes_total counter\n"
                  + "# UNIT counter_double_bytes_total bytes\n"
                  + $"counter_double_bytes_total{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',{additionalTags}key1='value1',key2='value2'}} 101.17 (\\d+)\n"
                  + "# EOF\n";

            Assert.Matches(("^" + expected + "$").Replace('\'', '"'), content);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        provider.Dispose();
    }
}
