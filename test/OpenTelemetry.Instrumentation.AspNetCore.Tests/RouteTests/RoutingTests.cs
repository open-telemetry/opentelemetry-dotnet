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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using RouteTests.TestApplication;
using Xunit;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace RouteTests;

public class RoutingTests : IClassFixture<RoutingTestFixture>
{
    private const string OldHttpStatusCode = "http.status_code";
    private const string OldHttpMethod = "http.method";
    private const string HttpStatusCode = "http.response.status_code";
    private const string HttpMethod = "http.request.method";
    private const string HttpRoute = "http.route";

    private readonly RoutingTestFixture fixture;
    private readonly List<Activity> exportedActivities = new();
    private readonly List<Metric> exportedMetrics = new();

    public RoutingTests(RoutingTestFixture fixture)
    {
        this.fixture = fixture;
    }

    public static IEnumerable<object[]> TestData => RoutingTestCases.GetTestCases();

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task TestHttpRoute(RoutingTestCases.TestCase testCase, bool useLegacyConventions)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [SemanticConventionOptInKeyName] = useLegacyConventions ? null : "http" })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(this.exportedActivities)
            .Build()!;

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddAspNetCoreInstrumentation()
            .AddInMemoryExporter(this.exportedMetrics)
            .Build()!;

        await this.fixture.MakeRequest(testCase.TestApplicationScenario, testCase.Path);

        for (var i = 0; i < 10; i++)
        {
            if (this.exportedActivities.Count > 0)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        meterProvider.ForceFlush();

        Assert.Single(this.exportedActivities);
        var durationMetric = this.exportedMetrics.Single(x => x.Name == "http.server.request.duration" || x.Name == "http.server.duration");

        var metricPoints = new List<MetricPoint>();
        foreach (var mp in durationMetric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);

        var activity = this.exportedActivities[0];
        var metricPoint = metricPoints.First();

        this.GetTagsFromActivity(useLegacyConventions, activity, out var activityHttpStatusCode, out var activityHttpMethod, out var activityHttpRoute);
        this.GetTagsFromMetricPoint(useLegacyConventions && Environment.Version.Major < 8, metricPoint, out var metricHttpStatusCode, out var metricHttpMethod, out var metricHttpRoute);

        Assert.Equal(testCase.ExpectedStatusCode, activityHttpStatusCode);
        Assert.Equal(testCase.ExpectedStatusCode, metricHttpStatusCode);
        Assert.Equal(testCase.HttpMethod, activityHttpMethod);
        Assert.Equal(testCase.HttpMethod, metricHttpMethod);

        // TODO: The CurrentActivityDisplayName, CurrentActivityHttpRoute, and CurrentMetricHttpRoute
        // properties will go away. They only serve to capture status quo. The "else" blocks are the real
        // asserts that we ultimately want.
        // If any of the current properties are null, then that means we already conform to the
        // correct behavior.
        if (testCase.CurrentActivityDisplayName != null)
        {
            Assert.Equal(testCase.CurrentActivityDisplayName, activity.DisplayName);
        }
        else
        {
            // Activity.DisplayName should be a combination of http.method + http.route attributes, see:
            // https://github.com/open-telemetry/semantic-conventions/blob/main/docs/http/http-spans.md#name
            var expectedActivityDisplayName = string.IsNullOrEmpty(testCase.ExpectedHttpRoute)
                ? testCase.HttpMethod
                : $"{testCase.HttpMethod} {testCase.ExpectedHttpRoute}";

            Assert.Equal(expectedActivityDisplayName, activity.DisplayName);
        }

        if (testCase.CurrentActivityHttpRoute != null)
        {
            Assert.Equal(testCase.CurrentActivityHttpRoute, activityHttpRoute);
        }
        else
        {
            Assert.Equal(testCase.ExpectedHttpRoute, activityHttpRoute);
        }

        if (testCase.CurrentMetricHttpRoute != null)
        {
            Assert.Equal(testCase.CurrentMetricHttpRoute, metricHttpRoute);
        }
        else
        {
            Assert.Equal(testCase.ExpectedHttpRoute, metricHttpRoute);
        }

        // Only produce README files based on final semantic conventions
        if (!useLegacyConventions)
        {
            var testResult = new RoutingTestResult
            {
                IdealHttpRoute = testCase.ExpectedHttpRoute,
                ActivityDisplayName = activity.DisplayName,
                ActivityHttpRoute = activityHttpRoute,
                MetricHttpRoute = metricHttpRoute,
                TestCase = testCase,
                RouteInfo = RouteInfo.Current,
            };

            this.fixture.AddTestResult(testResult);
        }
    }

    private void GetTagsFromActivity(bool useLegacyConventions, Activity activity, out int httpStatusCode, out string httpMethod, out string? httpRoute)
    {
        var expectedStatusCodeKey = useLegacyConventions ? OldHttpStatusCode : HttpStatusCode;
        var expectedHttpMethodKey = useLegacyConventions ? OldHttpMethod : HttpMethod;
        httpStatusCode = Convert.ToInt32(activity.GetTagItem(expectedStatusCodeKey));
        httpMethod = (activity.GetTagItem(expectedHttpMethodKey) as string)!;
        httpRoute = activity.GetTagItem(HttpRoute) as string ?? string.Empty;
    }

    private void GetTagsFromMetricPoint(bool useLegacyConventions, MetricPoint metricPoint, out int httpStatusCode, out string httpMethod, out string? httpRoute)
    {
        var expectedStatusCodeKey = useLegacyConventions ? OldHttpStatusCode : HttpStatusCode;
        var expectedHttpMethodKey = useLegacyConventions ? OldHttpMethod : HttpMethod;

        httpStatusCode = 0;
        httpMethod = string.Empty;
        httpRoute = string.Empty;

        foreach (var tag in metricPoint.Tags)
        {
            if (tag.Key.Equals(expectedStatusCodeKey))
            {
                httpStatusCode = Convert.ToInt32(tag.Value);
            }
            else if (tag.Key.Equals(expectedHttpMethodKey))
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
