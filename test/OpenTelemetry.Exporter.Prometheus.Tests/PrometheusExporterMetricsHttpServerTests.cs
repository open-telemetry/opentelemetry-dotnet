// <copyright file="PrometheusExporterMetricsHttpServerTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public class PrometheusExporterMetricsHttpServerTests
    {
        private const string MeterName = "PrometheusExporterMetricsHttpServerTests.Meter";

        [Fact]
        public async Task PrometheusExporterMetricsHttpServerIntegration()
        {
            Random random = new Random();
            int port = 0;
            int retryCount = 5;
            MeterProvider provider;
            string address = null;

            while (true)
            {
                try
                {
                    port = random.Next(2000, 5000);
                    provider = Sdk.CreateMeterProviderBuilder()
                        .AddMeter(MeterName)
                        .AddPrometheusExporter(o =>
                        {
                            o.GetUtcNowDateTimeOffset = () => new DateTimeOffset(2021, 9, 30, 22, 30, 0, TimeSpan.Zero);
#if NET461
                            bool expectedDefaultState = true;
#else
                            bool expectedDefaultState = false;
#endif
                            if (o.StartHttpListener != expectedDefaultState)
                            {
                                throw new InvalidOperationException("StartHttpListener value is unexpected.");
                            }

                            if (!o.StartHttpListener)
                            {
                                o.StartHttpListener = true;
                            }

                            address = $"http://localhost:{port}/";
                            o.HttpListenerPrefixes = new string[] { address };
                        })
                        .Build();
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.Message != PrometheusExporter.HttpListenerStartFailureExceptionMessage)
                    {
                        throw;
                    }

                    if (retryCount-- <= 0)
                    {
                        throw new InvalidOperationException("HttpListener could not be started.");
                    }
                }
            }

            var tags = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("key1", "value1"),
                new KeyValuePair<string, object>("key2", "value2"),
            };

            using var meter = new Meter(MeterName, "0.0.1");

            var counter = meter.CreateCounter<double>("counter_double");
            counter.Add(100.18D, tags);
            counter.Add(0.99D, tags);

            using HttpClient client = new HttpClient();

            using var response = await client.GetAsync($"{address}metrics").ConfigureAwait(false);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            Assert.Equal(
                $"# TYPE counter_double counter\ncounter_double{{key1=\"value1\",key2=\"value2\"}} 101.17 1633041000000\n",
                content);
        }
    }
}
