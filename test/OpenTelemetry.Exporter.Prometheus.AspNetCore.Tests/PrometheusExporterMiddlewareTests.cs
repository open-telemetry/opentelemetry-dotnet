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
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.Prometheus.Tests;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus.AspNetCore.Tests;

public sealed class PrometheusExporterMiddlewareTests
{
    private const string MeterName = nameof(PrometheusExporterMiddlewareTests);
    private const string MeterVersion = "1.0.1";

    [Fact]
    public async Task RunWithDefaultOptions()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint());

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunWithScopeInfoEnabledConfigured(bool scopeInfoEnabled)
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScopeInfoEnabled = scopeInfoEnabled),
            assertResponseContent: false);

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings).UseParameters(scopeInfoEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RunWithTargetInfoEnabledConfigured(bool targetInfoEnabled)
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.TargetInfoEnabled = targetInfoEnabled),
            assertResponseContent: false);

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings).UseParameters(targetInfoEnabled);
    }

    [Fact]
    public async Task RunWithCustomScrapeEndpointPath()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_options",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = "metrics_options"));

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithScrapeEndpointPathFallback()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            services => services.Configure<PrometheusAspNetCoreOptions>(o => o.ScrapeEndpointPath = null));

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithScrapeEndpointPathFallbackFromAddPrometheusExporter()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_from_AddPrometheusExporter",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            configureOptions: o =>
            {
                o.ScrapeEndpointPath = "/metrics_from_AddPrometheusExporter";
            });

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithScrapeEndpointPathOverride()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_override",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics_override"));

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithPathNamedOptionsOverride()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
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

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithHttpContextPredicate()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_predicate?enabled=true",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                httpContext => httpContext.Request.Path == "/metrics_predicate" && httpContext.Request.Query["enabled"] == "true"));

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithMixedPredicateAndPath()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
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

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithMixedPath()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
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

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithMeterProvider()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MeterName)
            .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1"))
            .AddPrometheusExporter()
            .Build();

        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(
                meterProvider: meterProvider,
                predicate: null,
                path: null,
                configureBranchedPipeline: null,
                optionsName: null),
            registerMeterProvider: false);

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithNoMetrics()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            skipMetrics: true);

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithMapPrometheusScrapingEndpoint()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint()),
            services => services.AddRouting());

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithMapPrometheusScrapingEndpointWithPathOverride()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics_path",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint("metrics_path")),
            services => services.AddRouting());

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithMapPrometheusScrapingEndpointWithPathNamedOptionsOverride()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
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

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithMapPrometheusScrapingEndpointWithMeterProvider()
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MeterName)
            .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1"))
            .AddPrometheusExporter()
            .Build();

        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseRouting().UseEndpoints(builder => builder.MapPrometheusScrapingEndpoint(
                path: null,
                meterProvider: meterProvider,
                configureBranchedPipeline: null,
                optionsName: null)),
            services => services.AddRouting(),
            registerMeterProvider: false);

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithTextPlainResponse()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "text/plain");

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithOpenMetricsVersionHeader()
    {
        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "application/openmetrics-text; version=1.0.0",
            contentType: "application/openmetrics-text; version=1.0.0; charset=utf-8; escaping=underscores");

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Theory]
    [MemberData(nameof(PrometheusAcceptHeaders.Valid), MemberType = typeof(PrometheusAcceptHeaders))]
    public void Negotiate_UsesTypedAcceptHeaders(
        string accept,
        string mediaType,
        bool isOpenMetrics,
        string version,
        string? escaping)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Accept = accept;

        var actual = PrometheusExporterMiddleware.Negotiate(context.Request.GetTypedHeaders());

        Assert.Equal(mediaType, actual.MediaType);
        Assert.Equal(isOpenMetrics, actual.IsOpenMetrics);
        Assert.Equal(Version.Parse(version), actual.Version);
        Assert.Equal(escaping, actual.Escaping);
    }

    [Theory]
    [MemberData(nameof(PrometheusAcceptHeaders.Invalid), MemberType = typeof(PrometheusAcceptHeaders))]
    public void Negotiate_UsesFallbackForInvalidHeader(string accept)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Accept = accept;

        var actual = PrometheusExporterMiddleware.Negotiate(context.Request.GetTypedHeaders());

        Assert.Equivalent(PrometheusProtocol.Fallback, actual);
    }

    [Fact]
    public async Task RunWithTextPlainResponseAndMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "text/plain",
            meterTags: meterTags);

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task RunWithOpenMetricsVersionHeaderAndMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        var output = await RunPrometheusExporterMiddlewareIntegrationTest(
            "/metrics",
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint(),
            acceptHeader: "application/openmetrics-text; version=1.0.0",
            contentType: "application/openmetrics-text; version=1.0.0; charset=utf-8; escaping=underscores",
            meterTags: meterTags);

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public Task CanServeMultipleContentTypes_NoMeterTags() =>
        RunPrometheusExporterMiddlewareIntegrationTestWithMultipleContentTypes();

    [Fact]
    public async Task CanServeMultipleContentTypes_WithMeterTags()
    {
        var meterTags = new KeyValuePair<string, object?>[]
        {
            new("meterKey1", "value1"),
            new("meterKey2", "value2"),
        };

        await RunPrometheusExporterMiddlewareIntegrationTestWithMultipleContentTypes(meterTags);
    }

    [Fact]
    public async Task BufferSizeIncreasesWithLotOfMetrics()
    {
        using var host = await StartTestHostAsync(
            app => app.UseOpenTelemetryPrometheusScrapingEndpoint());

        using var meter = new Meter(MeterName, MeterVersion);

        for (var x = 0; x < 1000; x++)
        {
            var counter = meter.CreateCounter<double>("counter_double_" + x, unit: "By");
            counter.Add(1);
        }

        host.Services.GetRequiredService<MeterProvider>().ForceFlush();

        using var client = host.GetTestClient();

        using var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));
        var output = await response.Content.ReadAsStringAsync();

        Assert.NotEmpty(output);

        await host.StopAsync();

        await Verify(output, "text", PrometheusSerializerTests.VerifySettings);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoData_Returns200()
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());
        exporter.Collect = _ => true;
        var middleware = new PrometheusExporterMiddleware(exporter);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionOccurs_Returns500()
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());
        exporter.Collect = _ => throw new InvalidOperationException("Simulated collection failure");
        var middleware = new PrometheusExporterMiddleware(exporter);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenExceptionOccursAfterResponseStarted_DoesNotReturn500()
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

    [Fact]
    public async Task InvokeAsync_WhenRequest_TimesOut_Returns408()
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());
        exporter.Collect = _ => true;
        var middleware = new PrometheusExporterMiddleware(exporter);

        var context = new DefaultHttpContext()
        {
            RequestAborted = new CancellationToken(canceled: true),
        };

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status408RequestTimeout, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("0.9")]
    [InlineData("1")]
    public async Task InvokeAsync_WhenRequestDeadlineExceeded_Returns408(string value)
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());

        exporter.Collect = _ =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(2));
            return true;
        };

        var middleware = new PrometheusExporterMiddleware(exporter);

        var context = new DefaultHttpContext();

        context.Request.Headers.Append("X-Prometheus-Scrape-Timeout-Seconds", value);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status408RequestTimeout, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("0.0009")]
    [InlineData("2147483")]
    [InlineData("2147483.1")]
    [InlineData("1.05e+003")]
    [InlineData("foo")]
    [InlineData("+Inf")]
    [InlineData("-Inf")]
    [InlineData("NaN")]
    public async Task InvokeAsync_WhenRequestDeadlineInvalid_Returns200(string scrapeTimeoutSeconds)
    {
        using var exporter = new PrometheusExporter(new PrometheusExporterOptions());
        exporter.Collect = _ => true;

        var middleware = new PrometheusExporterMiddleware(exporter);

        var context = new DefaultHttpContext();

        context.Request.Headers.Append("X-Prometheus-Scrape-Timeout-Seconds", scrapeTimeoutSeconds);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static async Task RunPrometheusExporterMiddlewareIntegrationTestWithMultipleContentTypes(
        KeyValuePair<string, object?>[]? meterTags = null,
        string? contentType = null)
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

        // Generate alternating formats
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
            await VerifyResponseAsync(response, testCase, meterTags, contentType);
        }

        await host.StopAsync();
    }

    private static async Task<string> RunPrometheusExporterMiddlewareIntegrationTest(
        string path,
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection>? configureServices = null,
        Action<HttpResponseMessage>? validateResponse = null,
        bool registerMeterProvider = true,
        Action<PrometheusAspNetCoreOptions>? configureOptions = null,
        bool skipMetrics = false,
        string acceptHeader = "application/openmetrics-text",
        string? contentType = null,
        KeyValuePair<string, object?>[]? meterTags = null,
        bool assertResponseContent = true)
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text", StringComparison.Ordinal);

        using var host = await StartTestHostAsync(
            configure,
            configureServices,
            registerMeterProvider,
            configureOptions);

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

        string responseContent;

        if (!skipMetrics)
        {
            var options = new PrometheusAspNetCoreOptions();
            configureOptions?.Invoke(options);

            responseContent = await VerifyResponseAsync(
                response,
                requestOpenMetrics,
                meterTags,
                contentType,
                assertResponseContent);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            responseContent = await response.Content.ReadAsStringAsync();
        }

        validateResponse?.Invoke(response);

        await host.StopAsync();
        return responseContent;
    }

    private static async Task<string> VerifyResponseAsync(
        HttpResponseMessage response,
        bool requestOpenMetrics,
        KeyValuePair<string, object?>[]? meterTags,
        string? contentType,
        bool assertResponseContent = true)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Content.Headers.Contains("Last-Modified"));

        contentType ??=
            requestOpenMetrics ?
            "application/openmetrics-text; version=0.0.1; charset=utf-8" :
            "text/plain; version=0.0.4; charset=utf-8";

        Assert.Equal(contentType, response.Content.Headers.ContentType!.ToString());
        Assert.Equal(["Accept-Encoding"], response.Headers.Vary);

        var content = (await response.Content.ReadAsStringAsync()).ReplaceLineEndings();

        if (assertResponseContent)
        {
            var additionalTags = meterTags is { Length: > 0 }
                ? $"{string.Join(",", meterTags.Select(x => $"otel_scope_{x.Key}=\"{x.Value}\""))},"
                : string.Empty;
            var createdMetricSample = requestOpenMetrics
                ? $"counter_double_bytes_created{{otel_scope_name=\"{MeterName}\",otel_scope_version=\"{MeterVersion}\",{additionalTags}key1=\"value1\",key2=\"value2\"}} [0-9]+(?:\\.[0-9]+)?"
                : string.Empty;

            var normalizedContent = content.ReplaceLineEndings();

            var expected = requestOpenMetrics
                ? $$"""
                    # TYPE target info
                    # HELP target Target metadata
                    target_info{service_name="my_service",service_instance_id="id1"} 1
                    # TYPE counter_double_bytes counter
                    # UNIT counter_double_bytes bytes
                    counter_double_bytes_total{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",{{additionalTags}}key1="value1",key2="value2"} 101.17
                    {{createdMetricSample}}
                    # EOF

                    """.ReplaceLineEndings()
                : $$"""
                    # TYPE target_info gauge
                    # HELP target_info Target metadata
                    target_info{service_name="my_service",service_instance_id="id1"} 1
                    # TYPE counter_double_bytes_total counter
                    # UNIT counter_double_bytes_total bytes
                    counter_double_bytes_total{otel_scope_name="{{MeterName}}",otel_scope_version="{{MeterVersion}}",{{additionalTags}}key1="value1",key2="value2"} 101.17
                    # EOF

                    """.ReplaceLineEndings();

            var matches = Regex.Matches(normalizedContent, "^" + expected + "$");

            Assert.True(matches.Count == 1, normalizedContent);
        }

        return content;
    }

    private static Task<IHost> StartTestHostAsync(
        Action<IApplicationBuilder> configure,
        Action<IServiceCollection>? configureServices = null,
        bool registerMeterProvider = true,
        Action<PrometheusAspNetCoreOptions>? configureOptions = null) =>
        new HostBuilder()
            .ConfigureLogging((logging) => logging.ClearProviders())
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
