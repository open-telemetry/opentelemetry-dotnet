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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.AspNetCore.Tests
{
    public sealed class PrometheusExporterMiddlewareTests
    {
        private static readonly string MeterName = Utils.GetCurrentMethodName();

        [Fact]
        public Task PrometheusExporterMiddlewareIntegration()
        {
            return RunPrometheusExporterMiddlewareIntegrationTest(
                "/metrics",
                app => app.UseOpenTelemetryPrometheusScrapingEndpoint());
        }

        [Fact]
        public Task PrometheusExporterMiddlewareIntegration_Options()
        {
            return RunPrometheusExporterMiddlewareIntegrationTest(
                "/metrics_options",
                app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
                services => services.Configure<PrometheusExporterOptions>(o => o.ScrapeEndpointPath = "metrics_options"));
        }

        [Fact]
        public Task PrometheusExporterMiddlewareIntegration_OptionsFallback()
        {
            return RunPrometheusExporterMiddlewareIntegrationTest(
                "/metrics",
                app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
                services => services.Configure<PrometheusExporterOptions>(o => o.ScrapeEndpointPath = null));
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
                        context.Response.Headers.Add("X-MiddlewareExecuted", "true");
                        return next();
                    })),
                services => services.Configure<PrometheusExporterOptions>(o => o.ScrapeEndpointPath = "/metrics_options"),
                validateResponse: rsp =>
                {
                    if (!rsp.Headers.TryGetValues("X-MiddlewareExecuted", out IEnumerable<string> headers))
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
                        context.Response.Headers.Add("X-MiddlewareExecuted", "true");
                        return next();
                    })),
                services => services.Configure<PrometheusExporterOptions>(o => o.ScrapeEndpointPath = "/metrics_options"),
                validateResponse: rsp =>
                {
                    if (!rsp.Headers.TryGetValues("X-MiddlewareExecuted", out IEnumerable<string> headers))
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
                .AddPrometheusExporter()
                .Build();

            await RunPrometheusExporterMiddlewareIntegrationTest(
                "/metrics",
                app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                    meterProvider: meterProvider,
                    predicate: null,
                    path: null,
                    configureBranchedPipeline: null),
                registerMeterProvider: false).ConfigureAwait(false);
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
        public async Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithMeterProvider()
        {
            using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(MeterName)
                .AddPrometheusExporter()
                .Build();

            await RunPrometheusExporterMiddlewareIntegrationTest(
                "/metrics",
                app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint(
                    path: null,
                    meterProvider: meterProvider,
                    configureBranchedPipeline: null)),
                services => services.AddRouting(),
                registerMeterProvider: false).ConfigureAwait(false);
        }

        private static async Task RunPrometheusExporterMiddlewareIntegrationTest(
            string path,
            Action<IApplicationBuilder> configure,
            Action<IServiceCollection> configureServices = null,
            Action<HttpResponseMessage> validateResponse = null,
            bool registerMeterProvider = true,
            Action<PrometheusExporterOptions> configureOptions = null,
            bool skipMetrics = false)
        {
            using var host = await new HostBuilder()
               .ConfigureWebHost(webBuilder => webBuilder
                   .UseTestServer()
                   .ConfigureServices(services =>
                   {
                       if (registerMeterProvider)
                       {
                           services.AddOpenTelemetryMetrics(builder => builder
                                .AddMeter(MeterName)
                                .AddPrometheusExporter(o =>
                                {
                                    configureOptions?.Invoke(o);
                                }));
                       }

                       configureServices?.Invoke(services);
                   })
                   .Configure(configure))
               .StartAsync();

            var tags = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("key1", "value1"),
                new KeyValuePair<string, object>("key2", "value2"),
            };

            using var meter = new Meter(MeterName);

            var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            var counter = meter.CreateCounter<double>("counter_double");
            if (!skipMetrics)
            {
                counter.Add(100.18D, tags);
                counter.Add(0.99D, tags);
            }

            using var response = await host.GetTestClient().GetAsync(path).ConfigureAwait(false);

            var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (!skipMetrics)
            {
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

                Assert.Equal('\n', content[^1]);

                var timestamp = long.Parse(content.Substring(index, content.Length - index - 1));

                Assert.True(beginTimestamp <= timestamp && timestamp <= endTimestamp);
            }
            else
            {
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }

            validateResponse?.Invoke(response);

            await host.StopAsync().ConfigureAwait(false);
        }
    }
}
#endif
