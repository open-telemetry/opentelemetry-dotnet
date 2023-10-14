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
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace RouteTests;

public class RoutingTests : IDisposable
{
    private const string HttpStatusCode = "http.status_code";
    private const string HttpMethod = "http.method";
    private const string HttpRoute = "http.route";

    private TracerProvider tracerProvider;
    private MeterProvider meterProvider;
    private WebApplication? app;
    private HttpClient client;
    private List<Activity> exportedActivities;
    private List<Metric> exportedMetrics;
    private AspNetCoreDiagnosticObserver diagnostics;

    public RoutingTests()
    {
        this.diagnostics = new AspNetCoreDiagnosticObserver();
        this.client = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

        this.exportedActivities = new List<Activity>();
        this.exportedMetrics = new List<Metric>();

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

#pragma warning disable xUnit1028
    [Theory]
    [MemberData(nameof(TestData))]
    public async Task<TestResult> TestRoutes(RouteTestData.RouteTestCase testCase, bool skipAsserts = true)
    {
        this.app = TestApplicationFactory.CreateApplication(testCase.TestApplicationScenario);
        var appTask = this.app.RunAsync();

        var responseMessage = await this.client.GetAsync(testCase.Path).ConfigureAwait(false);
        var response = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

        var info = JsonSerializer.Deserialize<RouteInfo>(response);

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

        return new TestResult
        {
            ActivityDisplayName = activity.DisplayName,
            HttpStatusCode = activityHttpStatusCode,
            HttpMethod = activityHttpMethod,
            HttpRoute = activityHttpRoute,
            RouteInfo = info!,
            TestCase = testCase,
        };
    }
#pragma warning restore xUnit1028

    public async void Dispose()
    {
        this.tracerProvider.Dispose();
        this.meterProvider.Dispose();
        this.diagnostics.Dispose();
        this.client.Dispose();
        if (this.app != null)
        {
            await this.app.DisposeAsync().ConfigureAwait(false);
        }
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
