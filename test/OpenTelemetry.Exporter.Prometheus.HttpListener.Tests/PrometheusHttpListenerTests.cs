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

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public class PrometheusHttpListenerTests
    {
        private readonly string meterName = Utils.GetCurrentMethodName();

        [Theory]
        [InlineData("http://example.com")]
        [InlineData("https://example.com")]
        [InlineData("http://127.0.0.1")]
        [InlineData("http://example.com", "https://example.com", "http://127.0.0.1")]
        public void ServerEndpointSanityCheckPositiveTest(params string[] uris)
        {
            using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddPrometheusHttpListener(options => options.Prefixes = uris)
                .Build();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("ftp://example.com")]
        [InlineData("http://example.com", "https://example.com", "ftp://example.com")]
        public void ServerEndpointSanityCheckNegativeTest(params string[] uris)
        {
            try
            {
                using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
                    .AddPrometheusHttpListener(options => options.Prefixes = uris)
                    .Build();
            }
            catch (Exception ex)
            {
                if (ex is not ArgumentNullException)
                {
                    Assert.Equal("System.ArgumentException", ex.GetType().ToString());
#if NETFRAMEWORK
                    Assert.Equal("Prometheus HttpListener prefix path should be a valid URI with http/https scheme.\r\nParameter name: prefixes", ex.Message);
#else
                    Assert.Equal("Prometheus HttpListener prefix path should be a valid URI with http/https scheme. (Parameter 'prefixes')", ex.Message);
#endif
                }
            }
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
                    .AddPrometheusHttpListener(options => options.Prefixes = new string[] { address })
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
                    "^# TYPE counter_double counter\ncounter_double{key1='value1',key2='value2'} 101.17 \\d+\n$".Replace('\'', '"'),
                    await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            else
            {
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }
        }
    }
}
