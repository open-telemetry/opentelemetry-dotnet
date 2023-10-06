// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class PrometheusHttpListenerTests
{
    private readonly string meterName = Utils.GetCurrentMethodName();

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
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddPrometheusHttpListener(options => options.UriPrefixes = uriPrefixes)
            .Build();
    }

    [Fact]
    public void UriPrefixesNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddPrometheusHttpListener(options => options.UriPrefixes = null)
                .Build();
        });
    }

    [Fact]
    public void UriPrefixesEmptyList()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddPrometheusHttpListener(options => options.UriPrefixes = new string[] { })
                .Build();
        });
    }

    [Fact]
    public void UriPrefixesInvalid()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddPrometheusHttpListener(options => options.UriPrefixes = new string[] { "ftp://example.com" })
                .Build();
        });
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest().ConfigureAwait(false);
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoMetrics()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest(skipMetrics: true).ConfigureAwait(false);
    }

    private async Task RunPrometheusExporterHttpServerIntegrationTest(bool skipMetrics = false)
    {
        Random random = new Random();
        int retryAttempts = 5;
        int port = 0;
        string address = null;

        MeterProvider provider;
        using var meter = new Meter(this.meterName);

        while (retryAttempts-- != 0)
        {
            port = random.Next(2000, 5000);
            address = $"http://localhost:{port}/";

            provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddPrometheusHttpListener(options => options.UriPrefixes = new string[] { address })
                .Build();
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
        using var response = await client.GetAsync($"{address}metrics").ConfigureAwait(false);

        if (!skipMetrics)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Content.Headers.Contains("Last-Modified"));
            Assert.Equal("text/plain; charset=utf-8; version=0.0.4", response.Content.Headers.ContentType.ToString());

            Assert.Matches(
                "^# TYPE counter_double_total counter\ncounter_double_total{key1='value1',key2='value2'} 101.17 \\d+\n\n# EOF\n$".Replace('\'', '"'),
                await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
