// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
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
    public Task PrometheusExporterMiddlewareIntegration() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint());

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_Options() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_options",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = "metrics_options"));

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_OptionsFallback() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = null));

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_OptionsViaAddPrometheusExporter() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_from_AddPrometheusExporter",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            configureOptions: o =>
            {
                o.ScrapeEndpointPath = "/metrics_from_AddPrometheusExporter";
            });

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_PathOverride() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_override",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics_override"));

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_WithPathNamedOptionsOverride() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
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

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_Predicate() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_predicate?enabled=true",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(httpcontext => httpcontext.Request.Path == "/metrics_predicate" && httpcontext.Request.Query["enabled"] == "true"));

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MixedPredicateAndPath() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
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
                if (!rsp.Headers.TryGetValues("X-MiddlewareExecuted", out var headers))
                {
                    headers = [];
                }

                Assert.Equal("true", headers.FirstOrDefault());
            });

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MixedPath() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
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
                if (!rsp.Headers.TryGetValues("X-MiddlewareExecuted", out var headers))
                {
                    headers = [];
                }

                Assert.Equal("true", headers.FirstOrDefault());
            });

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_MeterProvider()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
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
    public Task PrometheusExporterMiddlewareIntegration_NoMetrics() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            skipMetrics: true);

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MapEndpoint() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint()),
            services => services.AddRouting());

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithPathOverride() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_path",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint("metrics_path")),
            services => services.AddRouting());

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithPathNamedOptionsOverride() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
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

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_MapEndpoint_WithMeterProvider()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
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
    public Task PrometheusExporterMiddlewareIntegration_TextPlainResponse() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "text/plain");

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_UseOpenMetricsVersionHeader() =>
        RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "application/openmetrics-text; version=1.0.0");

    [Theory]
    [InlineData("application/openmetrics-text", true)]
    [InlineData("application/openmetrics-text; version=1.0.0", true)]
    [InlineData("application/openmetrics-text; version=\"1.0.0\"", true)]
    [InlineData("application/openmetrics-text; version=1.0.0; charset=utf-8", true)]
    [InlineData("Application/OpenMetrics-Text; version=1.0.0", true)]
    [InlineData("text/plain,application/openmetrics-text; version=1.0.0; charset=utf-8", true)]
    [InlineData("text/plain, application/openmetrics-text; version=1.0.0; charset=utf-8", true)]
    [InlineData("text/plain; charset=utf-8,application/openmetrics-text; version=1.0.0; charset=utf-8", true)]
    [InlineData("text/plain, */*;q=0.8,application/openmetrics-text; version=1.0.0; charset=utf-8", true)]
    [InlineData("text/plain; q=0.3, application/openmetrics-text; version=1.0.0; q=0.9", true)]
    [InlineData("TEXT/PLAIN; q=0.3, Application/OpenMetrics-Text; version=1.0.0; q=0.9", true)]
    [InlineData("application/openmetrics-text; version=0.0.1", false)]
    [InlineData("application/openmetrics-text; version=\"0.0.1\"", false)]
    [InlineData("application/openmetrics-text; version=0.0.1; charset=utf-8", false)]
    [InlineData("application/openmetrics-text; version=1.0.0; q=0", false)]
    [InlineData("text/plain", false)]
    [InlineData("text/plain; charset=utf-8", false)]
    [InlineData("text/plain; charset=utf-8; version=0.0.4", false)]
    [InlineData("text/plain; q=0.9, application/openmetrics-text; version=1.0.0; q=0.1", false)]
    [InlineData("TEXT/PLAIN; q=0.9, Application/OpenMetrics-Text; version=1.0.0; q=0.1", false)]
    [InlineData("text/plain; q=0, application/openmetrics-text; version=1.0.0; q=0", false)]
    [InlineData("*/*;q=0.8,text/plain; charset=utf-8; version=0.0.4", false)]
    public void PrometheusExporterMiddlewareAcceptsOpenMetrics_UsesTypedAcceptHeaders(string header, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Accept = header;

        var result = PrometheusExporterMiddleware.AcceptsOpenMetrics(context.Request);

        Assert.Equal(expected, result);
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_TextPlainResponse_WithMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "text/plain",
            meterTags: meterTags);
    }

    [Fact]
    public Task PrometheusExporterMiddlewareIntegration_UseOpenMetricsVersionHeader_WithMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        return RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "application/openmetrics-text; version=1.0.0",
            meterTags: meterTags);
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_CanServeOpenMetricsAndPlainFormats_NoMeterTags()
        => await RunPrometheusExporterMiddlewareIntegrationTestWithBothFormats();

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_CanServeOpenMetricsAndPlainFormats_WithMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        await RunPrometheusExporterMiddlewareIntegrationTestWithBothFormats(meterTags);
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareIntegration_TestBufferSizeIncrease_With_LotOfMetrics()
    {
        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint());

        using var meter = new Meter(MeterName, MeterVersion);

        for (var x = 0; x < 1000; x++)
        {
            var counter = meter.CreateCounter<double>("counter_double_" + x, unit: "By");
            counter.Add(1);
        }

        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));
        var text = await response.Content.ReadAsStringAsync();

        Assert.NotEmpty(text);

        await host.StopAsync();
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareInvokeAsync_WhenNoData_Returns200()
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());
        exporter.Collect = _ => true;
        var middleware = new PrometheusExporterMiddleware(exporter);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareInvokeAsync_WhenExceptionOccurs_Returns500()
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());
        exporter.Collect = _ => throw new InvalidOperationException("Simulated collection failure");
        var middleware = new PrometheusExporterMiddleware(exporter);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task PrometheusExporterMiddlewareInvokeAsync_WhenExceptionOccursAfterResponseStarted_DoesNotReturn500()
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());
        exporter.Collect = _ => throw new InvalidOperationException("Simulated collection failure");
        var middleware = new PrometheusExporterMiddleware(exporter);

        var context = new DefaultHttpContext();

        // Replace the response feature so HasStarted returns true, simulating response headers already committed.
        context.Features.Set<IHttpResponseFeature>(new AlreadyStartedHttpResponseFeature());

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static async Task RunPrometheusExporterMiddlewareIntegrationTestWithBothFormats(KeyValuePair<string, object?>[]? meterTags = null)
    {
        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint());

        var counterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        using var meter = new Meter(MeterName, MeterVersion, meterTags);

        var counter = meter.CreateCounter<double>("counter_double", unit: "By");
        counter.Add(100.18D, counterTags);
        counter.Add(0.99D, counterTags);

        var testCases = new bool[] { true, false, true, true, false };

        using var client = host.GetTestClient();

        foreach (var testCase in testCases)
        {
            using var request = new HttpRequestMessage
            {
                Headers = { { "Accept", testCase ? "application/openmetrics-text" : "text/plain" } },
                RequestUri = new Uri("/metrics", UriKind.Relative),
                Method = HttpMethod.Get,
            };
            using var response = await client.SendAsync(request);
            await VerifyAsync(response, testCase, meterTags);
        }

        await host.StopAsync();
    }

    private static async Task RunPrometheusExporterMiddlewareIntegrationTest(
        string path,
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection>? configureServices = null,
        Action<HttpResponseMessage>? validateResponse = null,
        bool registerMeterProvider = true,
        Action<PrometheusAspNetCoreOptions>? configureOptions = null,
        bool skipMetrics = false,
        string acceptHeader = "application/openmetrics-text",
        KeyValuePair<string, object?>[]? meterTags = null)
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text", StringComparison.Ordinal);

        using var host = await StartTestHostAsync(configure, configureServices, registerMeterProvider, configureOptions);

        var counterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        using var meter = new Meter(MeterName, MeterVersion, meterTags);

        var counter = meter.CreateCounter<double>("counter_double", unit: "By");
        if (!skipMetrics)
        {
            counter.Add(100.18D, counterTags);
            counter.Add(0.99D, counterTags);
        }

        using var client = host.GetTestClient();

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        if (!skipMetrics)
        {
            var options = new PrometheusAspNetCoreOptions();
            configureOptions?.Invoke(options);
            await VerifyAsync(response, requestOpenMetrics, meterTags);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        validateResponse?.Invoke(response);

        await host.StopAsync();
    }

    private static async Task VerifyAsync(HttpResponseMessage response, bool requestOpenMetrics, KeyValuePair<string, object?>[]? meterTags)
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

        var additionalTags = meterTags is { Length: > 0 }
            ? $"{string.Join(",", meterTags.Select(x => $"{x.Key}=\"{x.Value}\""))},"
            : string.Empty;
        var createdMetricSample = requestOpenMetrics
            ? $"\ncounter_double_bytes_created{{otel_scope_name=\"{MeterName}\",otel_scope_version=\"{MeterVersion}\",{additionalTags}key1=\"value1\",key2=\"value2\"}} [0-9]+(?:\\.[0-9]+)?"
            : string.Empty;

        var content = (await response.Content.ReadAsStringAsync()).ReplaceLineEndings();

        var expected = requestOpenMetrics
            ? $$"""
                    # TYPE target info
                    # HELP target Target metadata
                    target_info{service_name="my_service",service_instance_id="id1"} 1
                    # TYPE otel_scope_info info
                    # HELP otel_scope_info Scope metadata
                    otel_scope_info{otel_scope_name="{{MeterName}}"} 1
                    # TYPE counter_double_bytes counter
                    # UNIT counter_double_bytes bytes
                    counter_double_bytes_total{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",{{additionalTags}}key1="value1",key2="value2"} 101.17{{createdMetricSample}}
                    # EOF

                    """.ReplaceLineEndings()
            : $$"""
                    # TYPE counter_double_bytes_total counter
                    # UNIT counter_double_bytes_total bytes
                    counter_double_bytes_total{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",{{additionalTags}}key1="value1",key2="value2"} 101.17
                    # EOF

                    """.ReplaceLineEndings();

        var matches = Regex.Matches(content, "^" + expected + "$");

        Assert.True(matches.Count == 1, content);
    }

    private static Task<IHost> StartTestHostAsync(
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection>? configureServices = null,
        bool registerMeterProvider = true,
        Action<PrometheusAspNetCoreOptions>? configureOptions = null) =>
        new HostBuilder()
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

    private sealed class AlreadyStartedHttpResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
#endif
