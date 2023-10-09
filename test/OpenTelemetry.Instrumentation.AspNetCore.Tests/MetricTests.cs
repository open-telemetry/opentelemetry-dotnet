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

#if !NET8_0_OR_GREATER
using System.Diagnostics;
#endif
#if NET8_0_OR_GREATER
using Microsoft.AspNetCore.Builder;
#endif
using Microsoft.AspNetCore.Hosting;
#if !NET8_0_OR_GREATER
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.AspNetCore.Mvc.Testing;
#if !NET8_0_OR_GREATER
using Microsoft.AspNetCore.TestHost;
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

        this.meterProvider.Dispose();

        var requestDurationMetric = metricItems
            .Where(item => item.Name == "http.server.request.duration")
            .ToArray();

        var activeRequestsMetric = metricItems.
            Where(item => item.Name == "http.server.active_requests")
            .ToArray();

        var routeMatchingMetric = metricItems.
            Where(item => item.Name == "aspnetcore.routing.match_attempts")
            .ToArray();

        var kestrelActiveConnectionsMetric = metricItems.
            Where(item => item.Name == "kestrel.active_connections")
            .ToArray();
        var kestrelQueuedConnectionMetric = metricItems.
            Where(item => item.Name == "kestrel.queued_connections")
            .ToArray();

        Assert.Single(requestDurationMetric);
        Assert.Single(activeRequestsMetric);
        Assert.Single(routeMatchingMetric);
        Assert.Single(kestrelActiveConnectionsMetric);
        Assert.Single(kestrelQueuedConnectionMetric);

        // TODO
        // kestrel.queued_requests
        // kestrel.upgraded_connections
        // kestrel.rejected_connections
        // kestrel.tls_handshake.duration
        // kestrel.active_tls_handshakes

        await app.DisposeAsync();
    }
#endif

    [Fact]
    public async Task RequestMetricIsCaptured_New()
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
            .Where(item => item.Name == "http.server.request.duration")
            .ToArray();

        var metric = Assert.Single(requestMetrics);

        Assert.Equal("s", metric.Unit);
        var metricPoints = GetMetricPoints(metric);
        Assert.Equal(2, metricPoints.Count);

        AssertMetricPoints_New(
            metricPoints: metricPoints,
            expectedRoutes: new List<string> { "api/Values", "api/Values/{id}" },
            expectedTagsCount: 6);
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
            expectedTagsCount: 6);
    }

    [Fact]
    public async Task MetricNotCollectedWhenFilterIsApplied()
    {
        var metricItems = new List<Metric>();

        void ConfigureTestServices(IServiceCollection services)
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddAspNetCoreInstrumentation(opt => opt.Filter = (name, ctx) => ctx.Request.Path != "/api/values/2")
                .AddInMemoryExporter(metricItems)
                .Build();
        }

        using (var client = this.factory
                   .WithWebHostBuilder(builder =>
                   {
                       builder.ConfigureTestServices(ConfigureTestServices);
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

        // Assert single because we filtered out one route
        var metricPoint = Assert.Single(GetMetricPoints(metric));
        AssertMetricPoint_Old(metricPoint);
    }

    [Fact]
    public async Task MetricEnrichedWithCustomTags()
    {
        var tagsToAdd = new KeyValuePair<string, object>[]
        {
            new("custom_tag_1", 1),
            new("custom_tag_2", "one"),
        };

        var metricItems = new List<Metric>();

        void ConfigureTestServices(IServiceCollection services)
        {
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddAspNetCoreInstrumentation(opt => opt.Enrich = (string _, HttpContext _, ref TagList tags) =>
                {
                    foreach (var keyValuePair in tagsToAdd)
                    {
                        tags.Add(keyValuePair);
                    }
                })
                .AddInMemoryExporter(metricItems)
                .Build();
        }

        using (var client = this.factory
                   .WithWebHostBuilder(builder =>
                   {
                       builder.ConfigureTestServices(ConfigureTestServices);
                       builder.ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                   })
                   .CreateClient())
        {
            using var response = await client.GetAsync("/api/values").ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
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
        var metricPoint = Assert.Single(GetMetricPoints(metric));

        var tags = AssertMetricPoint_Old(metricPoint, expectedTagsCount: StandardTagsCount + 2);

        Assert.Contains(tagsToAdd[0], tags);
        Assert.Contains(tagsToAdd[1], tags);
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
                AssertMetricPoint_New(metricPoint.Value, expectedRoute, expectedTagsCount);
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

        var method = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRequestMethod, "GET");
        var scheme = new KeyValuePair<string, object>(SemanticConventions.AttributeUrlScheme, "http");
        var statusCode = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpResponseStatusCode, 200);
        var flavor = new KeyValuePair<string, object>(SemanticConventions.AttributeNetworkProtocolVersion, "1.1");
        var route = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRoute, expectedRoute);
        Assert.Contains(method, attributes);
        Assert.Contains(scheme, attributes);
        Assert.Contains(statusCode, attributes);
        Assert.Contains(flavor, attributes);
        Assert.Contains(route, attributes);

        // Inspect Histogram Bounds
        var histogramBuckets = metricPoint.GetHistogramBuckets();
        var histogramBounds = new List<double>();
        foreach (var t in histogramBuckets)
        {
            histogramBounds.Add(t.ExplicitBound);
        }

        Assert.Equal(
            expected: new List<double> { 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, double.PositiveInfinity },
            actual: histogramBounds);

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
