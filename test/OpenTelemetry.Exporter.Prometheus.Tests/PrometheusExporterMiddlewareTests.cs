// <copyright file="PrometheusExporterMiddlewareTests.cs" company="OpenTelemetry Authors">
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

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public sealed class PrometheusExporterMiddlewareTests
    {
        private static readonly string MeterName = Utils.GetCurrentMethodName();

        [Fact]
        public async Task PrometheusExporterMiddlewareIntegration()
        {
            var host = await new HostBuilder()
               .ConfigureWebHost(webBuilder => webBuilder
                   .UseTestServer()
                   .UseStartup<Startup>())
               .StartAsync();

            var tags = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("key1", "value1"),
                new KeyValuePair<string, object>("key2", "value2"),
            };

            using var meter = new Meter(MeterName);

            var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            var counter = meter.CreateCounter<double>("counter_double");
            counter.Add(100.18D, tags);
            counter.Add(0.99D, tags);

            using var response = await host.GetTestClient().GetAsync("/metrics").ConfigureAwait(false);

            var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Content.Headers.Contains("Last-Modified"));
            Assert.Equal("text/plain; charset=utf-8; version=0.0.4", response.Content.Headers.ContentType.ToString());

            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            string[] lines = content.Split('\n');

            Assert.Equal(
                $"# TYPE counter_double counter",
                lines[0]);

            Assert.Contains(
                $"counter_double{{key1=\"value1\",key2=\"value2\"}} 101.17",
                lines[1]);

            var index = content.LastIndexOf(' ');

            Assert.Equal('\n', content[content.Length - 1]);

            var timestamp = long.Parse(content.Substring(index, content.Length - index - 1));

            Assert.True(beginTimestamp <= timestamp && timestamp <= endTimestamp);

            await host.StopAsync().ConfigureAwait(false);
        }

        public class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddOpenTelemetryMetrics(builder => builder
                    .AddMeter(MeterName)
                    .AddPrometheusExporter(o =>
                    {
                        if (o.StartHttpListener)
                        {
                            throw new InvalidOperationException("StartHttpListener should be false on .NET Core 3.1+.");
                        }
                    }));
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseOpenTelemetryPrometheusScrapingEndpoint();
            }
        }
    }
}
#endif
