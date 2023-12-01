// <copyright file="PrometheusHttpListenerTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;

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
        await this.RunPrometheusExporterHttpServerIntegrationTest(useOpenMetrics: false);
    }

    private async Task RunPrometheusExporterHttpServerIntegrationTest(bool skipMetrics = false, bool useOpenMetrics = true)
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
                .AddPrometheusHttpListener(options =>
                {
                    options.OpenMetricsEnabled = useOpenMetrics;
                    options.UriPrefixes = new string[] { address };
                })
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

        if (useOpenMetrics)
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/openmetrics-text"));
        }

        using var response = await client.GetAsync($"{address}metrics");

        if (!skipMetrics)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Content.Headers.Contains("Last-Modified"));

            if (useOpenMetrics)
            {
                Assert.Equal("application/openmetrics-text; version=1.0.0; charset=utf-8", response.Content.Headers.ContentType.ToString());
            }
            else
            {
                Assert.Equal("text/plain; charset=utf-8; version=0.0.4", response.Content.Headers.ContentType.ToString());
            }

            var content = await response.Content.ReadAsStringAsync();

            var expected = useOpenMetrics
                ? "# TYPE counter_double_total counter\n"
                  + "counter_double_total{key1='value1',key2='value2'} 101.17 \\d+\\.\\d{3}\n"
                  + "# EOF\n"
                : "# TYPE counter_double_total counter\n"
                  + "counter_double_total{key1='value1',key2='value2'} 101.17 \\d+\n"
                  + "# EOF\n";

            Assert.Matches(("^" + expected + "$").Replace('\'', '"'), content);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
