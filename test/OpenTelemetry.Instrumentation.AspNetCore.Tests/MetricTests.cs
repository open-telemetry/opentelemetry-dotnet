// <copyright file="MetricTests.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests;

public class MetricTests
    : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
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
        var res = await client.GetStringAsync("http://localhost:5000/");
        Assert.NotNull(res);

        // We need to let metric callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the callbacks to complete
        await Task.Delay(TimeSpan.FromSeconds(1));

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
        var res = await client.GetStringAsync("http://localhost:5000/");
        Assert.NotNull(res);

        // We need to let metric callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the callbacks to complete
        await Task.Delay(TimeSpan.FromSeconds(1));

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
    public async Task RequestMetricIsCaptured(string api, string expectedRoute, string expectedErrorType, int expectedStatusCode)
    {
        var metricItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
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
                using var response = await client.GetAsync(api);
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
        await Task.Delay(TimeSpan.FromSeconds(1));

        this.meterProvider.Dispose();

        var requestMetrics = metricItems
            .Where(item => item.Name == "http.server.request.duration")
            .ToArray();

        var metric = Assert.Single(requestMetrics);

        Assert.Equal("s", metric.Unit);
        var metricPoints = GetMetricPoints(metric);
        Assert.Single(metricPoints);

        AssertMetricPoints(
            metricPoints: metricPoints,
            expectedRoutes: new List<string> { expectedRoute },
            expectedErrorType,
            expectedStatusCode,
            expectedTagsCount: expectedErrorType == null ? 5 : 6);
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
        var metricItems = new List<Metric>();

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
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
            using var response = await client.SendAsync(message);
        }
        catch
        {
            // ignore error.
        }

        // We need to let End callback execute as it is executed AFTER response was returned.
        // In unit tests environment there may be a lot of parallel unit tests executed, so
        // giving some breezing room for the End callback to complete
        await Task.Delay(TimeSpan.FromSeconds(1));

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

    private static void AssertMetricPoints(
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
                AssertMetricPoint(metricPoint.Value, expectedStatusCode, expectedRoute, expectedErrorType, expectedTagsCount);
            }
            else
            {
                Assert.Fail($"A metric for route '{expectedRoute}' was not found");
            }
        }
    }

    private static void AssertMetricPoint(
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
            var errorType = new KeyValuePair<string, object>(SemanticConventions.AttributeErrorType, expectedErrorType);

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
    }
}
