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
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Metrics;
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
        await this.RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: string.Empty);
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoOpenMetrics_NoScopeInfo()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: string.Empty, scopeInfoEnabled: false);
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_UseOpenMetricsVersionHeader()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: "application/openmetrics-text; version=1.0.0");
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoScopeInfo()
    {
        await this.RunPrometheusExporterHttpServerIntegrationTest(scopeInfoEnabled: false);
    }

    private static string GetExpectedContent(bool requestedOpenMetrics, bool scopeInfoEnabled)
    {
        if (requestedOpenMetrics && scopeInfoEnabled)
        {
            return "# TYPE otel_scope_info info\n"
                   + "# HELP otel_scope_info Scope metadata\n"
                   + $"otel_scope_info{{otel_scope_name='{MeterName}'}} 1\n"
                   + "# TYPE counter_double_total counter\n"
                   + $"counter_double_total{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',key1='value1',key2='value2'}} 101.17 (\\d+\\.\\d{{3}})\n"
                   + "# EOF\n";
        }

        if (!requestedOpenMetrics && scopeInfoEnabled)
        {
            return "# TYPE counter_double_total counter\n"
                   + $"counter_double_total{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',key1='value1',key2='value2'}} 101.17 (\\d+)\n"
                   + "# EOF\n";
        }

        if (requestedOpenMetrics && !scopeInfoEnabled)
        {
            return "# TYPE counter_double_total counter\n"
                   + "counter_double_total{key1='value1',key2='value2'} 101.17 (\\d+\\.\\d{3})\n"
                   + "# EOF\n";
        }

        return "# TYPE counter_double_total counter\n"
               + "counter_double_total{key1='value1',key2='value2'} 101.17 (\\d+)\n"
               + "# EOF\n";
    }

    private async Task RunPrometheusExporterHttpServerIntegrationTest(bool skipMetrics = false, string acceptHeader = "application/openmetrics-text", bool scopeInfoEnabled = true)
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
                    .AddPrometheusHttpListener(options =>
                    {
                        options.ScopeInfoEnabled = scopeInfoEnabled;
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
            var expected = GetExpectedContent(requestOpenMetrics, scopeInfoEnabled);

            Assert.Matches(("^" + expected + "$").Replace('\'', '"'), content);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        provider.Dispose();
    }
}
