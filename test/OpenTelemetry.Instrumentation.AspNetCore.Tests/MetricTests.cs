// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET8_0_OR_GREATER
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
#endif
using Microsoft.AspNetCore.Hosting;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.AspNetCore.Mvc.Testing;
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.RateLimiting;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

public class MetricTests
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    public const string SemanticConventionOptInKeyName = "OTEL_SEMCONV_STABILITY_OPT_IN";

    private const int StandardTagsCount = 6;

    private readonly WebApplicationFactory<Program> factory;
    private MeterProvider meterProvider;

    public MetricTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void AddAspNetCoreInstrumentation_BadArgs()
    {
        MeterProviderBuilder builder = null;
        Assert.Throws<ArgumentNullException>(() => builder.AddAspNetCoreInstrumentation());
    }

#if NET8_0_OR_GREATER
    [Fact]
    public async Task ValidateNet8MetricsAsync()
    {
        var metricItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.MapGet("/", () => "Hello");

        _ = app.RunAsync();

        using var client = new HttpClient();
        var res = await client.GetStringAsync("http://localhost:5000/").ConfigureAwait(false);
        Assert.NotNull(res);

        // We need to let metric callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the callbacks to complete
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        this.meterProvider.Dispose();

        var requestDurationMetric = metricItems
            .Count(item => item.Name == "http.server.request.duration");

        var activeRequestsMetric = metricItems.
            Count(item => item.Name == "http.server.active_requests");

        var routeMatchingMetric = metricItems.
            Count(item => item.Name == "aspnetcore.routing.match_attempts");

        var kestrelActiveConnectionsMetric = metricItems.
            Count(item => item.Name == "kestrel.active_connections");

        var kestrelQueuedConnectionMetric = metricItems.
            Count(item => item.Name == "kestrel.queued_connections");

        Assert.Equal(1, requestDurationMetric);
        Assert.Equal(1, activeRequestsMetric);
        Assert.Equal(1, routeMatchingMetric);
        Assert.Equal(1, kestrelActiveConnectionsMetric);
        Assert.Equal(1, kestrelQueuedConnectionMetric);

        // TODO
        // kestrel.queued_requests
        // kestrel.upgraded_connections
        // kestrel.rejected_connections
        // kestrel.tls_handshake.duration
        // kestrel.active_tls_handshakes

        await app.DisposeAsync();
    }

    [Fact]
    public async Task ValidateNet8RateLimitingMetricsAsync()
    {
        var metricItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRateLimiter(_ => _
        .AddFixedWindowLimiter(policyName: "fixed", options =>
        {
            options.PermitLimit = 4;
            options.Window = TimeSpan.FromSeconds(12);
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            options.QueueLimit = 2;
        }));

        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.UseRateLimiter();

        static string GetTicks() => (DateTime.Now.Ticks & 0x11111).ToString("00000");

        app.MapGet("/", () => Results.Ok($"Hello {GetTicks()}"))
                                   .RequireRateLimiting("fixed");

        _ = app.RunAsync();

        using var client = new HttpClient();
        var res = await client.GetStringAsync("http://localhost:5000/").ConfigureAwait(false);
        Assert.NotNull(res);

        // We need to let metric callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the callbacks to complete
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        this.meterProvider.Dispose();

        var activeRequestleasesMetric = metricItems
            .Where(item => item.Name == "aspnetcore.rate_limiting.active_request_leases")
            .ToArray();

        var requestLeaseDurationMetric = metricItems.
            Where(item => item.Name == "aspnetcore.rate_limiting.request_lease.duration")
            .ToArray();

        var limitingRequestsMetric = metricItems.
            Where(item => item.Name == "aspnetcore.rate_limiting.requests")
            .ToArray();

        Assert.Single(activeRequestleasesMetric);
        Assert.Single(requestLeaseDurationMetric);
        Assert.Single(limitingRequestsMetric);

        // TODO
        // aspnetcore.rate_limiting.request.time_in_queue
        // aspnetcore.rate_limiting.queued_requests

        await app.DisposeAsync();
    }
#endif

    [Theory]
    [InlineData("/api/values/2", "api/Values/{id}", null, 200)]
    [InlineData("/api/Error", "api/Error", "System.Exception", 500)]
    public async Task RequestMetricIsCaptured_New(string api, string expectedRoute, string expectedErrorType, int expectedStatusCode)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http" })
            .Build();

        var metricItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            try
            {
                using var response = await client.GetAsync(api).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // ignore error.
            }
        }

        // We need to let End callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the End callback to complete
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        this.meterProvider.Dispose();

        var requestMetrics = metricItems
            .Where(item => item.Name == "http.server.request.duration")
            .ToArray();

        var metric = Assert.Single(requestMetrics);

        Assert.Equal("s", metric.Unit);
        var metricPoints = GetMetricPoints(metric);
        Assert.Single(metricPoints);

        AssertMetricPoints_New(
            metricPoints: metricPoints,
            expectedRoutes: new List<string> { expectedRoute },
            expectedErrorType,
            expectedStatusCode,
            expectedTagsCount: expectedErrorType == null ? 6 : 7);
    }

    [Theory]
    [InlineData("CONNECT", "CONNECT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("GET", "GET")]
    [InlineData("PUT", "PUT")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("OPTIONS", "OPTIONS")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("Get", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("TRACE", "TRACE")]
    [InlineData("CUSTOM", "_OTHER")]
    public async Task HttpRequestMethodIsCapturedAsPerSpec(string originalMethod, string expectedMethod)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http" })
            .Build();

        var metricItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        using var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient();

        var message = new HttpRequestMessage();
        message.Method = new HttpMethod(originalMethod);

        try
        {
            using var response = await client.SendAsync(message).ConfigureAwait(false);
        }
        catch
        {
            // ignore error.
        }

        // We need to let End callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the End callback to complete
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        this.meterProvider.Dispose();

        var requestMetrics = metricItems
            .Where(item => item.Name == "http.server.request.duration")
            .ToArray();

        var metric = Assert.Single(requestMetrics);

        Assert.Equal("s", metric.Unit);
        var metricPoints = GetMetricPoints(metric);
        Assert.Single(metricPoints);

        var mp = metricPoints[0];

        // Inspect Metric Attributes
        var attributes = new Dictionary<string, object>();
        foreach (var tag in mp.Tags)
        {
            attributes[tag.Key] = tag.Value;
        }

        Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == expectedMethod);

        Assert.DoesNotContain(attributes, t => t.Key == SemanticConventions.AttributeHttpRequestMethodOriginal);
    }

#if !NET8_0_OR_GREATER
    [Fact]
    public async Task RequestMetricIsCaptured_Old()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = null })
            .Build();

        var metricItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            using var response1 = await client.GetAsync("/api/values").ConfigureAwait(false);
            using var response2 = await client.GetAsync("/api/values/2").ConfigureAwait(false);

            response1.EnsureSuccessStatusCode();
            response2.EnsureSuccessStatusCode();
        }

        // We need to let End callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the End callback to complete
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        this.meterProvider.Dispose();

        var requestMetrics = metricItems
            .Where(item => item.Name == "http.server.duration")
            .ToArray();

        var metric = Assert.Single(requestMetrics);
        Assert.Equal("ms", metric.Unit);
        var metricPoints = GetMetricPoints(metric);
        Assert.Equal(2, metricPoints.Count);

        AssertMetricPoints_Old(
            metricPoints: metricPoints,
            expectedRoutes: new List<string> { "api/Values", "api/Values/{id}" },
            expectedTagsCount: 6);
    }

    [Fact]
    public async Task RequestMetricIsCaptured_Dup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http/dup" })
            .Build();

        var metricItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        using (var client = this.factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
            })
            .CreateClient())
        {
            using var response1 = await client.GetAsync("/api/values").ConfigureAwait(false);
            using var response2 = await client.GetAsync("/api/values/2").ConfigureAwait(false);

            response1.EnsureSuccessStatusCode();
            response2.EnsureSuccessStatusCode();
        }

        // We need to let End callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the End callback to complete
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

        this.meterProvider.Dispose();

        // Validate Old Semantic Convention
        var requestMetrics = metricItems
            .Where(item => item.Name == "http.server.duration")
            .ToArray();

        var metric = Assert.Single(requestMetrics);
        Assert.Equal("ms", metric.Unit);
        var metricPoints = GetMetricPoints(metric);
        Assert.Equal(2, metricPoints.Count);

        AssertMetricPoints_Old(
            metricPoints: metricPoints,
            expectedRoutes: new List<string> { "api/Values", "api/Values/{id}" },
            expectedTagsCount: 6);

        // Validate New Semantic Convention
        requestMetrics = metricItems
            .Where(item => item.Name == "http.server.request.duration")
            .ToArray();

        metric = Assert.Single(requestMetrics);

        Assert.Equal("s", metric.Unit);
        metricPoints = GetMetricPoints(metric);
        Assert.Equal(2, metricPoints.Count);

        AssertMetricPoints_New(
            metricPoints: metricPoints,
            expectedRoutes: new List<string> { "api/Values", "api/Values/{id}" },
            null,
            200,
            expectedTagsCount: 6);
    }
#endif

    public void Dispose()
    {
        this.meterProvider?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static List<MetricPoint> GetMetricPoints(Metric metric)
    {
        Assert.NotNull(metric);
        Assert.True(metric.MetricType == MetricType.Histogram);
        var metricPoints = new List<MetricPoint>();
        foreach (var p in metric.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        return metricPoints;
    }

    private static void AssertMetricPoints_New(
        List<MetricPoint> metricPoints,
        List<string> expectedRoutes,
        string expectedErrorType,
        int expectedStatusCode,
        int expectedTagsCount)
    {
        // Assert that one MetricPoint exists for each ExpectedRoute
        foreach (var expectedRoute in expectedRoutes)
        {
            MetricPoint? metricPoint = null;

            foreach (var mp in metricPoints)
            {
                foreach (var tag in mp.Tags)
                {
                    if (tag.Key == SemanticConventions.AttributeHttpRoute && tag.Value.ToString() == expectedRoute)
                    {
                        metricPoint = mp;
                    }
                }
            }

            if (metricPoint.HasValue)
            {
                AssertMetricPoint_New(metricPoint.Value, expectedStatusCode, expectedRoute, expectedErrorType, expectedTagsCount);
            }
            else
            {
                Assert.Fail($"A metric for route '{expectedRoute}' was not found");
            }
        }
    }

    private static void AssertMetricPoints_Old(
        List<MetricPoint> metricPoints,
        List<string> expectedRoutes,
        int expectedTagsCount)
    {
        // Assert that one MetricPoint exists for each ExpectedRoute
        foreach (var expectedRoute in expectedRoutes)
        {
            MetricPoint? metricPoint = null;

            foreach (var mp in metricPoints)
            {
                foreach (var tag in mp.Tags)
                {
                    if (tag.Key == SemanticConventions.AttributeHttpRoute && tag.Value.ToString() == expectedRoute)
                    {
                        metricPoint = mp;
                    }
                }
            }

            if (metricPoint.HasValue)
            {
                AssertMetricPoint_Old(metricPoint.Value, expectedRoute, expectedTagsCount);
            }
            else
            {
                Assert.Fail($"A metric for route '{expectedRoute}' was not found");
            }
        }
    }

    private static KeyValuePair<string, object>[] AssertMetricPoint_New(
        MetricPoint metricPoint,
        int expectedStatusCode,
        string expectedRoute,
        string expectedErrorType,
        int expectedTagsCount)
    {
        var count = metricPoint.GetHistogramCount();
        var sum = metricPoint.GetHistogramSum();

        Assert.Equal(1L, count);
        Assert.True(sum > 0);

        var attributes = new KeyValuePair<string, object>[metricPoint.Tags.Count];
        int i = 0;
        foreach (var tag in metricPoint.Tags)
        {
            attributes[i++] = tag;
        }

        // Inspect Attributes
        Assert.Equal(expectedTagsCount, attributes.Length);

        var method = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRequestMethod, "GET");
        var scheme = new KeyValuePair<string, object>(SemanticConventions.AttributeUrlScheme, "http");
        var statusCode = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpResponseStatusCode, expectedStatusCode);
        var flavor = new KeyValuePair<string, object>(SemanticConventions.AttributeNetworkProtocolVersion, "1.1");
        var route = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRoute, expectedRoute);
        Assert.Contains(method, attributes);
        Assert.Contains(scheme, attributes);
        Assert.Contains(statusCode, attributes);
        Assert.Contains(flavor, attributes);
        Assert.Contains(route, attributes);

        if (expectedErrorType != null)
        {
#if NET8_0_OR_GREATER
            // Expected to change in next release
            // https://github.com/dotnet/aspnetcore/issues/51029
            var errorType = new KeyValuePair<string, object>("exception.type", expectedErrorType);
#else
            var errorType = new KeyValuePair<string, object>(SemanticConventions.AttributeErrorType, expectedErrorType);
#endif
            Assert.Contains(errorType, attributes);
        }

        // Inspect Histogram Bounds
        var histogramBuckets = metricPoint.GetHistogramBuckets();
        var histogramBounds = new List<double>();
        foreach (var t in histogramBuckets)
        {
            histogramBounds.Add(t.ExplicitBound);
        }

        // TODO: Remove the check for the older bounds once 1.7.0 is released. This is a temporary fix for instrumentation libraries CI workflow.

        var expectedHistogramBoundsOld = new List<double> { 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, double.PositiveInfinity };
        var expectedHistogramBoundsNew = new List<double> { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, double.PositiveInfinity };

        var histogramBoundsMatchCorrectly = Enumerable.SequenceEqual(expectedHistogramBoundsOld, histogramBounds) ||
            Enumerable.SequenceEqual(expectedHistogramBoundsNew, histogramBounds);

        Assert.True(histogramBoundsMatchCorrectly);

        return attributes;
    }

    private static KeyValuePair<string, object>[] AssertMetricPoint_Old(
        MetricPoint metricPoint,
        string expectedRoute = "api/Values",
        int expectedTagsCount = StandardTagsCount)
    {
        var count = metricPoint.GetHistogramCount();
        var sum = metricPoint.GetHistogramSum();

        Assert.Equal(1L, count);
        Assert.True(sum > 0);

        var attributes = new KeyValuePair<string, object>[metricPoint.Tags.Count];
        int i = 0;
        foreach (var tag in metricPoint.Tags)
        {
            attributes[i++] = tag;
        }

        // Inspect Attributes
        Assert.Equal(expectedTagsCount, attributes.Length);

        var method = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, "GET");
        var scheme = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, "http");
        var statusCode = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, 200);
        var flavor = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, "1.1");
        var host = new KeyValuePair<string, object>(SemanticConventions.AttributeNetHostName, "localhost");
        var route = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRoute, expectedRoute);
        Assert.Contains(method, attributes);
        Assert.Contains(scheme, attributes);
        Assert.Contains(statusCode, attributes);
        Assert.Contains(flavor, attributes);
        Assert.Contains(host, attributes);
        Assert.Contains(route, attributes);

        // Inspect Histogram Bounds
        var histogramBuckets = metricPoint.GetHistogramBuckets();
        var histogramBounds = new List<double>();
        foreach (var t in histogramBuckets)
        {
            histogramBounds.Add(t.ExplicitBound);
        }

        Assert.Equal(
            expected: new List<double> { 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000, double.PositiveInfinity },
            actual: histogramBounds);

        return attributes;
    }
}
