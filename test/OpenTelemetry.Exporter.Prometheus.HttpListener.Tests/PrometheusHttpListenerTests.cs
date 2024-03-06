// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
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
            TestPrometheusHttpListenerUriPrefixOptions(null);
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
    public void PrometheusHttpListenerThrowsOnStart()
    {
        Random random = new Random();
        int retryAttempts = 5;
        int port = 0;
        string address = null;

        PrometheusExporter exporter = null;
        PrometheusHttpListener listener = null;

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
                    UriPrefixes = new string[] { address },
                });

            listener.Start();
        });

        exporter?.Dispose();
        listener?.Dispose();
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

    private async Task RunPrometheusExporterHttpServerIntegrationTest(bool skipMetrics = false, string acceptHeader = "application/openmetrics-text")
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text");

        Random random = new Random();
        int retryAttempts = 5;
        int port = 0;
        string address = null;

        MeterProvider provider = null;
        using var meter = new Meter(MeterName, MeterVersion);

        while (retryAttempts-- != 0)
        {
            port = random.Next(2000, 5000);
            address = $"http://localhost:{port}/";

            try
            {
                provider = Sdk.CreateMeterProviderBuilder()
                    .AddMeter(meter.Name)
                    .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1"))
                    .AddPrometheusHttpListener(options =>
                    {
                        options.UriPrefixes = new string[] { address };
                    })
                    .Build();

                break;
            }
            catch
            {
                // ignored
            }
        }

        if (provider == null)
        {
            throw new InvalidOperationException("HttpListener could not be started");
        }

        var tags = new KeyValuePair<string, object>[]
        {
            new KeyValuePair<string, object>("key1", "value1"),
            new KeyValuePair<string, object>("key2", "value2"),
        };

        var counter = meter.CreateCounter<double>("counter_double");
        if (!skipMetrics)
        {
            counter.Add(100.18D, tags);
            counter.Add(0.99D, tags);
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
                Assert.Equal("application/openmetrics-text; version=1.0.0; charset=utf-8", response.Content.Headers.ContentType.ToString());
            }
            else
            {
                Assert.Equal("text/plain; charset=utf-8; version=0.0.4", response.Content.Headers.ContentType.ToString());
            }

            var content = await response.Content.ReadAsStringAsync();

            var expected = requestOpenMetrics
                ? "# TYPE target info\n"
                  + "# HELP target Target metadata\n"
                  + "target_info{service_name='my_service',service_instance_id='id1'} 1\n"
                  + "# TYPE otel_scope_info info\n"
                  + "# HELP otel_scope_info Scope metadata\n"
                  + $"otel_scope_info{{otel_scope_name='{MeterName}'}} 1\n"
                  + "# TYPE counter_double_total counter\n"
                  + $"counter_double_total{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',key1='value1',key2='value2'}} 101.17 (\\d+\\.\\d{{3}})\n"
                  + "# EOF\n"
                : "# TYPE counter_double_total counter\n"
                  + $"counter_double_total{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',key1='value1',key2='value2'}} 101.17 (\\d+)\n"
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
