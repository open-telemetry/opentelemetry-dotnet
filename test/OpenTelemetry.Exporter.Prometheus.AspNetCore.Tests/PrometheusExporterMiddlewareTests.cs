// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.AspNetCore.Tests;

public sealed class PrometheusExporterMiddlewareTests
{
    private const string MeterVersion = "1.0.1";

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
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = "metrics_options"));
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_OptionsFallback()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = null));
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
    public Task PrometheusExporterMiddlewareIntegration_WithPathNamedOptionsOverride()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_override",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                meterProvider: null,
                predicate: null,
                path: null,
                configureBranchedPipeline: null,
                optionsName: "myOptions"),
            services =>
            {
                services.Configure<PrometheusAspNetCoreOptions>("myOptions", o => o.ScrapeEndpointPath = "/metrics_override");
            });
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
                    context.Response.Headers.Append("X-MiddlewareExecuted", "true");
                    return next();
                }),
                optionsName: null),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = "/metrics_options"),
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
                    context.Response.Headers.Append("X-MiddlewareExecuted", "true");
                    return next();
                }),
                optionsName: null),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = "/metrics_options"),
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
            .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1"))
            .AddPrometheusExporter()
            .Build();

        await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                meterProvider: meterProvider,
                predicate: null,
                path: null,
                configureBranchedPipeline: null,
                optionsName: null),
            registerMeterProvider: false);
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
    public Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithPathNamedOptionsOverride()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_path",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint(
                path: null,
                meterProvider: null,
                configureBranchedPipeline: null,
                optionsName: "myOptions")),
            services =>
            {
                services.AddRouting();
                services.Configure<PrometheusAspNetCoreOptions>("myOptions", o => o.ScrapeEndpointPath = "/metrics_path");
            });
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithMeterProvider()
    {
        using MeterProvider meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MeterName)
            .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1"))
            .AddPrometheusExporter()
            .Build();

        await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint(
                path: null,
                meterProvider: meterProvider,
                configureBranchedPipeline: null,
                optionsName: null)),
            services => services.AddRouting(),
            registerMeterProvider: false);
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_TextPlainResponse()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "text/plain");
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_UseOpenMetricsVersionHeader()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "application/openmetrics-text; version=1.0.0");
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_AddResourceAttributesAsTags()
    {
        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            configureOptions: o => o.AllowedResourceAttributesFilter = s => s == "service.name",
            addServiceNameResourceTag: true);
    }

    private static async Task RunPrometheusExporterMiddlewareIntegrationTest(
        string path,
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection> configureServices = null,
        Action<HttpResponseMessage> validateResponse = null,
        bool registerMeterProvider = true,
        Action<PrometheusAspNetCoreOptions> configureOptions = null,
        bool skipMetrics = false,
        string acceptHeader = "application/openmetrics-text",
        bool addServiceNameResourceTag = false)
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text");

        using var host = await new HostBuilder()
           .ConfigureWebHost(webBuilder => webBuilder
               .UseTestServer()
               .ConfigureServices(services =>
               {
                   if (registerMeterProvider)
                   {
                       services.AddOpenTelemetry().WithMetrics(builder => builder
                           .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1"))
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

        using var meter = new Meter(MeterName, MeterVersion);

        var beginTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        var counter = meter.CreateCounter<double>("counter_double");
        if (!skipMetrics)
        {
            counter.Add(100.18D, tags);
            counter.Add(0.99D, tags);
        }

        using var client = host.GetTestClient();

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync(path);

        var endTimestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();

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

            string content = await response.Content.ReadAsStringAsync();

            var resourceTagAttributes = addServiceNameResourceTag
                ? "service_name='my_service',"
                : string.Empty;

            string expected = requestOpenMetrics
                ? "# TYPE target info\n"
                  + "# HELP target Target metadata\n"
                  + "target_info{service_name='my_service',service_instance_id='id1'} 1\n"
                  + "# TYPE otel_scope_info info\n"
                  + "# HELP otel_scope_info Scope metadata\n"
                  + $"otel_scope_info{{otel_scope_name='{MeterName}'}} 1\n"
                  + "# TYPE counter_double_total counter\n"
                  + $"counter_double_total{{{resourceTagAttributes}otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',key1='value1',key2='value2'}} 101.17 (\\d+\\.\\d{{3}})\n"
                  + "# EOF\n"
                : "# TYPE counter_double_total counter\n"
                  + $"counter_double_total{{{resourceTagAttributes}otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',key1='value1',key2='value2'}} 101.17 (\\d+)\n"
                  + "# EOF\n";

            var matches = Regex.Matches(content, ("^" + expected + "$").Replace('\'', '"'));

            Assert.Single(matches);

            var timestamp = long.Parse(matches[0].Groups[1].Value.Replace(".", string.Empty));

            Assert.True(beginTimestamp <= timestamp && timestamp <= endTimestamp);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        validateResponse?.Invoke(response);

        await host.StopAsync();
    }
}
#endif
