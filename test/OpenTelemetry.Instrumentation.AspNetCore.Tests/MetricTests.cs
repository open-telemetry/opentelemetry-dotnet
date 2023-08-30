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

using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
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

    [Theory]
    [InlineData(null, true, false, 6, 0)] // emits old metric & attributes
    [InlineData("http", false, true, 0, 5)] // emits new metric & attributes
    [InlineData("http/dup", true, true, 6, 5)] // emits both old & new
    public async Task RequestMetricIsCaptured(string environmentVarValue, bool validateOldSemConv, bool validateNewSemConv, int expectedOldTagsCount, int expectedNewTagsCount)
    {
        try
        {
            Environment.SetEnvironmentVariable(SemanticConventionOptInKeyName, environmentVarValue);

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

            if (validateOldSemConv)
            {
                var requestMetrics = metricItems
                    .Where(item => item.Name == "http.server.duration")
                    .ToArray();

                var metric = Assert.Single(requestMetrics);
                Assert.Equal("ms", metric.Unit);
                var metricPoints = GetMetricPoints(metric);
                Assert.Equal(2, metricPoints.Count);

                // AssertMetricPoint(metricPoints[0], expectedRoute: "api/Values", expectedTagsCount: expectedOldTagsCount, validateOldSemConv: true);
                // AssertMetricPoint(metricPoints[1], expectedRoute: "api/Values/{id}", expectedTagsCount: expectedOldTagsCount, validateOldSemConv: true);
                AssertMetricPoint(GetMetricPoint(metricPoints, "api/Values").Value, expectedRoute: "api/Values", expectedTagsCount: expectedOldTagsCount, validateOldSemConv: true);
                AssertMetricPoint(GetMetricPoint(metricPoints, "api/Values/{id}").Value, expectedRoute: "api/Values/{id}", expectedTagsCount: expectedOldTagsCount, validateOldSemConv: true);
            }

            if (validateNewSemConv)
            {
                var requestMetrics = metricItems
                    .Where(item => item.Name == "http.server.request.duration")
                    .ToArray();

                var metric = Assert.Single(requestMetrics);

                // TODO: This needs to be changed to "s" (seconds). This is blocked until we can change the default histogram.
                // See: https://github.com/open-telemetry/opentelemetry-dotnet/issues/4797
                Assert.Equal("ms", metric.Unit);
                var metricPoints = GetMetricPoints(metric);
                Assert.Equal(2, metricPoints.Count);

                // AssertMetricPoint(metricPoints[0], expectedRoute: "api/Values", expectedTagsCount: expectedNewTagsCount, validateNewSemConv: true);
                // AssertMetricPoint(metricPoints[1], expectedRoute: "api/Values/{id}", expectedTagsCount: expectedNewTagsCount, validateNewSemConv: true);
                AssertMetricPoint(GetMetricPoint(metricPoints, "api/Values").Value, expectedRoute: "api/Values", expectedTagsCount: expectedNewTagsCount, validateNewSemConv: true);
                AssertMetricPoint(GetMetricPoint(metricPoints, "api/Values/{id}").Value, expectedRoute: "api/Values/{id}", expectedTagsCount: expectedNewTagsCount, validateNewSemConv: true);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(SemanticConventionOptInKeyName, null);
        }
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
        AssertMetricPoint(metricPoint, validateOldSemConv: true);
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

        var tags = AssertMetricPoint(metricPoint, expectedTagsCount: StandardTagsCount + 2, validateOldSemConv: true);

        Assert.Contains(tagsToAdd[0], tags);
        Assert.Contains(tagsToAdd[1], tags);
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

    private static MetricPoint? GetMetricPoint(List<MetricPoint> metricPoints, string expectedRoute)
    {
        for (int i = 0; i < metricPoints.Count; i++)
        {
            var metricPoint = metricPoints[i];

            foreach (var tag in metricPoint.Tags)
            {
                if (tag.Key == SemanticConventions.AttributeHttpRoute && tag.Value.ToString() == expectedRoute)
                {
                    return metricPoint;
                }
            }
        }

        Assert.Fail("MetricPoint not found");
        return null;
    }

    private static KeyValuePair<string, object>[] AssertMetricPoint(
        MetricPoint metricPoint,
        string expectedRoute = "api/Values",
        int expectedTagsCount = StandardTagsCount,
        bool validateNewSemConv = false,
        bool validateOldSemConv = false)
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

        if (validateNewSemConv)
        {
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
        }

        if (validateOldSemConv)
        {
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
        }

        // Inspect Histogram Bounds
        var histogramBuckets = metricPoint.GetHistogramBuckets();
        var histogramBounds = new List<double>();
        foreach (var t in histogramBuckets)
        {
            histogramBounds.Add(t.ExplicitBound);
        }

        // TODO: This will need to test for the new histograms. This is blocked until we can change the default histogram.
        // See: https://github.com/open-telemetry/opentelemetry-dotnet/issues/4797
        Assert.Equal(
            expected: new List<double> { 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000, double.PositiveInfinity },
            actual: histogramBounds);

        return attributes;
    }
}
