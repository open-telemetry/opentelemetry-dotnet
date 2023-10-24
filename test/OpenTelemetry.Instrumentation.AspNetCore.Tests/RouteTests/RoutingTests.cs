// <copyright file="RoutingTests.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace RouteTests;

public class RoutingTests : IClassFixture<RoutingTestFixture>, IDisposable
{
    // TODO: This test currently uses the old conventions. Update it to use the new conventions.
    private const string HttpStatusCode = "http.status_code";
    private const string HttpMethod = "http.method";
    private const string HttpRoute = "http.route";

    private readonly RoutingTestFixture fixture;
    private readonly TracerProvider tracerProvider;
    private readonly MeterProvider meterProvider;
    private readonly List<Activity> exportedActivities = new();
    private readonly List<Metric> exportedMetrics = new();

    public RoutingTests(RoutingTestFixture fixture)
    {
        this.fixture = fixture;

        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(this.exportedActivities)
            .Build()!;

        this.meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(this.exportedMetrics)
            .Build()!;
    }

    public static IEnumerable<object[]> TestData => RouteTestData.GetTestCases();

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task TestRoutes(RouteTestData.RouteTestCase testCase, bool skipAsserts = true)
    {
        await this.fixture.MakeRequest(testCase.TestApplicationScenario, testCase.Path);

        for (var i = 0; i < 10; i++)
        {
            if (this.exportedActivities.Count > 0)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        this.meterProvider.ForceFlush();

        Assert.Single(this.exportedActivities);
        Assert.Single(this.exportedMetrics);

        var metricPoints = new List<MetricPoint>();
        foreach (var mp in this.exportedMetrics[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);

        var activity = this.exportedActivities[0];
        var metricPoint = metricPoints.First();

        this.GetTagsFromActivity(activity, out var activityHttpStatusCode, out var activityHttpMethod, out var activityHttpRoute);
        this.GetTagsFromMetricPoint(metricPoint, out var metricHttpStatusCode, out var metricHttpMethod, out var metricHttpRoute);

        Assert.Equal(testCase.ExpectedStatusCode, activityHttpStatusCode);
        Assert.Equal(testCase.ExpectedStatusCode, metricHttpStatusCode);
        Assert.Equal(testCase.HttpMethod, activityHttpMethod);
        Assert.Equal(testCase.HttpMethod, metricHttpMethod);

        if (!skipAsserts)
        {
            Assert.Equal(testCase.ExpectedHttpRoute, activityHttpRoute);
            Assert.Equal(testCase.ExpectedHttpRoute, metricHttpRoute);

            var expectedActivityDisplayName = string.IsNullOrEmpty(testCase.ExpectedHttpRoute)
                ? testCase.HttpMethod
                : $"{testCase.HttpMethod} {testCase.ExpectedHttpRoute}";
            Assert.Equal(expectedActivityDisplayName, activity.DisplayName);
        }

        var testResult = new TestResult
        {
            ActivityDisplayName = activity.DisplayName,
            HttpStatusCode = activityHttpStatusCode,
            HttpMethod = activityHttpMethod,
            HttpRoute = activityHttpRoute,
            RouteInfo = null,
            TestCase = testCase,
        };

        this.fixture.AddTestResult(testResult);
    }

    public void Dispose()
    {
        this.tracerProvider.Dispose();
        this.meterProvider.Dispose();
    }

    private void GetTagsFromActivity(Activity activity, out int httpStatusCode, out string httpMethod, out string? httpRoute)
    {
        httpStatusCode = Convert.ToInt32(activity.GetTagItem(HttpStatusCode));
        httpMethod = (activity.GetTagItem(HttpMethod) as string)!;
        httpRoute = activity.GetTagItem(HttpRoute) as string ?? string.Empty;
    }

    private void GetTagsFromMetricPoint(MetricPoint metricPoint, out int httpStatusCode, out string httpMethod, out string? httpRoute)
    {
        httpStatusCode = 0;
        httpMethod = string.Empty;
        httpRoute = string.Empty;

        foreach (var tag in metricPoint.Tags)
        {
            if (tag.Key.Equals(HttpStatusCode))
            {
                httpStatusCode = Convert.ToInt32(tag.Value);
            }
            else if (tag.Key.Equals(HttpMethod))
            {
                httpMethod = (tag.Value as string)!;
            }
            else if (tag.Key.Equals(HttpRoute))
            {
                httpRoute = tag.Value as string;
            }
        }
    }
}
