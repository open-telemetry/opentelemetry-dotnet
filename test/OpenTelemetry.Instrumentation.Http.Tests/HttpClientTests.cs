// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NETFRAMEWORK
using System.Net.Http;
#endif
#if !NET8_0_OR_GREATER
using System.Reflection;
using System.Text.Json;
#endif
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public partial class HttpClientTests
{
    public static readonly IEnumerable<object[]> TestData = HttpTestData.ReadTestCases();

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyTracesAndMetricsSemanticConventionsAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: true,
            enableMetrics: true);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyMetricsOnlyAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: false,
            enableMetrics: true);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyTracesOnlyAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: true,
            enableMetrics: false);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyNoSignalsAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: false,
            enableMetrics: false);
    }

#if !NET8_0_OR_GREATER
    [Fact]
    public async Task DebugIndividualTestAsync()
    {
        var input = JsonSerializer.Deserialize<HttpTestData.HttpOutTestCase[]>(
            @"
                [
                  {
                    ""name"": ""Response code: 399"",
                    ""method"": ""GET"",
                    ""url"": ""http://{host}:{port}/"",
                    ""responseCode"": 399,
                    ""responseExpected"": true,
                    ""spanName"": ""GET"",
                    ""spanStatus"": ""Unset"",
                    ""spanKind"": ""Client"",
                    ""spanAttributes"": {
                      ""url.scheme"": ""http"",
                      ""http.request.method"": ""GET"",
                      ""server.address"": ""{host}"",
                      ""server.port"": ""{port}"",
                      ""http.response.status_code"": ""399"",
                      ""network.protocol.version"": ""{flavor}"",
                      ""url.full"": ""http://{host}:{port}/""
                    }
                  }
                ]
                ",
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var t = (Task)this.GetType().InvokeMember(nameof(this.HttpOutCallsAreCollectedSuccessfullyTracesAndMetricsSemanticConventionsAsync), BindingFlags.InvokeMethod, null, this, HttpTestData.GetArgumentsFromTestCaseObject(input).First());
        await t;
    }
#endif

    [Fact]
    public async Task CheckEnrichmentWhenSampling()
    {
        await CheckEnrichment(new AlwaysOffSampler(), false, this.url);
        await CheckEnrichment(new AlwaysOnSampler(), true, this.url);
    }

#if NET8_0_OR_GREATER
    [Theory]
    [MemberData(nameof(TestData))]
    public async Task ValidateNet8MetricsAsync(HttpTestData.HttpOutTestCase tc)
    {
        var metrics = new List<Metric>();
        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        var testUrl = HttpTestData.NormalizeValues(tc.Url, this.host, this.port);

        try
        {
            using var c = new HttpClient();
            using var request = new HttpRequestMessage
            {
                RequestUri = new Uri(testUrl),
                Method = new HttpMethod(tc.Method),
            };

            request.Headers.Add("contextRequired", "false");
            request.Headers.Add("responseCode", (tc.ResponseCode == 0 ? 200 : tc.ResponseCode).ToString());
            await c.SendAsync(request);
        }
        catch (Exception)
        {
            // test case can intentionally send request that will result in exception
        }
        finally
        {
            meterProvider.Dispose();
        }

        var requestMetrics = metrics
            .Where(metric =>
            metric.Name == "http.client.request.duration" ||
            metric.Name == "http.client.active_requests" ||
            metric.Name == "http.client.request.time_in_queue" ||
            metric.Name == "http.client.connection.duration" ||
            metric.Name == "http.client.open_connections" ||
            metric.Name == "dns.lookup.duration")
            .ToArray();

        if (tc.ResponseExpected)
        {
            Assert.Equal(6, requestMetrics.Count());
        }
        else
        {
            // http.client.connection.duration and http.client.open_connections will not be emitted.
            Assert.Equal(4, requestMetrics.Count());
        }
    }
#endif

    private static async Task HttpOutCallsAreCollectedSuccessfullyBodyAsync(
        string host,
        int port,
        HttpTestData.HttpOutTestCase tc,
        bool enableTracing,
        bool enableMetrics)
    {
        bool enrichWithHttpWebRequestCalled = false;
        bool enrichWithHttpWebResponseCalled = false;
        bool enrichWithHttpRequestMessageCalled = false;
        bool enrichWithHttpResponseMessageCalled = false;
        bool enrichWithExceptionCalled = false;

        var testUrl = HttpTestData.NormalizeValues(tc.Url, host, port);

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder();

        if (enableMetrics)
        {
            meterProviderBuilder
                .AddHttpClientInstrumentation();
        }

        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder();

        if (enableTracing)
        {
            tracerProviderBuilder
                .AddHttpClientInstrumentation((opt) =>
                {
                    opt.EnrichWithHttpWebRequest = (activity, httpRequestMessage) => { enrichWithHttpWebRequestCalled = true; };
                    opt.EnrichWithHttpWebResponse = (activity, httpResponseMessage) => { enrichWithHttpWebResponseCalled = true; };
                    opt.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) => { enrichWithHttpRequestMessageCalled = true; };
                    opt.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) => { enrichWithHttpResponseMessageCalled = true; };
                    opt.EnrichWithException = (activity, exception) => { enrichWithExceptionCalled = true; };
                    opt.RecordException = tc.RecordException ?? false;
                });
        }

        var metrics = new List<Metric>();
        var activities = new List<Activity>();

        var meterProvider = meterProviderBuilder
            .AddInMemoryExporter(metrics)
            .Build();

        var tracerProvider = tracerProviderBuilder
            .AddInMemoryExporter(activities)
            .Build();

        try
        {
            using var c = new HttpClient();
            using var request = new HttpRequestMessage
            {
                RequestUri = new Uri(testUrl),
                Method = new HttpMethod(tc.Method),
            };

            if (tc.Headers != null)
            {
                foreach (var header in tc.Headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            request.Headers.Add("contextRequired", "false");
            request.Headers.Add("responseCode", (tc.ResponseCode == 0 ? 200 : tc.ResponseCode).ToString());

            await c.SendAsync(request);
        }
        catch (Exception)
        {
            // test case can intentionally send request that will result in exception
        }
        finally
        {
            tracerProvider.Dispose();
            meterProvider.Dispose();
        }

        var requestMetrics = metrics
            .Where(metric => metric.Name == "http.client.request.duration")
            .ToArray();

        var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, host, port));

        if (!enableTracing)
        {
            Assert.Empty(activities);
        }
        else
        {
            var activity = Assert.Single(activities);

            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal(tc.SpanName, activity.DisplayName);

#if NETFRAMEWORK
            Assert.True(enrichWithHttpWebRequestCalled);
            Assert.False(enrichWithHttpRequestMessageCalled);
            if (tc.ResponseExpected)
            {
                Assert.True(enrichWithHttpWebResponseCalled);
                Assert.False(enrichWithHttpResponseMessageCalled);
            }
#else
            Assert.False(enrichWithHttpWebRequestCalled);
            Assert.True(enrichWithHttpRequestMessageCalled);
            if (tc.ResponseExpected)
            {
                Assert.False(enrichWithHttpWebResponseCalled);
                Assert.True(enrichWithHttpResponseMessageCalled);
            }
#endif

            // Assert.Equal(tc.SpanStatus, d[span.Status.CanonicalCode]);
            Assert.Equal(tc.SpanStatus, activity.Status.ToString());
            Assert.Null(activity.StatusDescription);

            var normalizedAttributes = activity.TagObjects.Where(kv => !kv.Key.StartsWith("otel.")).ToDictionary(x => x.Key, x => x.Value.ToString());

            int numberOfTags = activity.Status == ActivityStatusCode.Error ? 5 : 4;

            var expectedAttributeCount = numberOfTags + (tc.ResponseExpected ? 2 : 0);

            Assert.Equal(expectedAttributeCount, normalizedAttributes.Count);

            Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpRequestMethod]);
            Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeServerAddress]);
            Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeServerPort]);
            Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeUrlFull && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeUrlFull]);
            if (tc.ResponseExpected)
            {
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetworkProtocolVersion]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpResponseStatusCode]);

                if (tc.ResponseCode >= 400)
                {
                    Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeErrorType && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpResponseStatusCode]);
                }
            }
            else
            {
                Assert.DoesNotContain(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode);
                Assert.DoesNotContain(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion);

#if NET8_0_OR_GREATER
                // we are using fake address so it will be "name_resolution_error"
                // TODO: test other error types.
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeErrorType && kvp.Value.ToString() == "name_resolution_error");
#elif NETFRAMEWORK
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeErrorType && kvp.Value.ToString() == "name_resolution_failure");
#else
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeErrorType && kvp.Value.ToString() == "System.Net.Http.HttpRequestException");
#endif
            }

            if (tc.RecordException.HasValue && tc.RecordException.Value)
            {
                Assert.Single(activity.Events.Where(evt => evt.Name.Equals("exception")));
                Assert.True(enrichWithExceptionCalled);
            }
        }

        if (!enableMetrics)
        {
            Assert.Empty(requestMetrics);
        }
        else
        {
            Assert.Single(requestMetrics);

            var metric = requestMetrics.FirstOrDefault(m => m.Name == "http.client.request.duration");
            Assert.NotNull(metric);
            Assert.Equal("s", metric.Unit);
            Assert.True(metric.MetricType == MetricType.Histogram);

            var metricPoints = new List<MetricPoint>();
            foreach (var p in metric.GetMetricPoints())
            {
                metricPoints.Add(p);
            }

            Assert.Single(metricPoints);
            var metricPoint = metricPoints[0];

            var count = metricPoint.GetHistogramCount();
            var sum = metricPoint.GetHistogramSum();

            Assert.Equal(1L, count);

            if (enableTracing)
            {
                var activity = Assert.Single(activities);
#if !NET8_0_OR_GREATER
                Assert.Equal(activity.Duration.TotalSeconds, sum);
#endif
            }
            else
            {
                Assert.True(sum > 0);
            }

            // Inspect Metric Attributes
            var attributes = new Dictionary<string, object>();
            foreach (var tag in metricPoint.Tags)
            {
                attributes[tag.Key] = tag.Value;
            }

            var numberOfTags = 4;
            if (tc.ResponseExpected)
            {
                var expectedStatusCode = int.Parse(normalizedAttributesTestCase[SemanticConventions.AttributeHttpResponseStatusCode]);
                numberOfTags = (expectedStatusCode >= 400) ? 5 : 4; // error.type extra tag
            }
            else
            {
                numberOfTags = 5; // error.type would be extra
            }

            var expectedAttributeCount = numberOfTags + (tc.ResponseExpected ? 2 : 0); // responsecode + protocolversion

            Assert.Equal(expectedAttributeCount, attributes.Count);

            Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpRequestMethod]);
            Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeServerAddress]);
            Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeServerPort]);
            Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeUrlScheme && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeUrlScheme]);

            if (tc.ResponseExpected)
            {
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetworkProtocolVersion]);
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpResponseStatusCode]);

                if (tc.ResponseCode >= 400)
                {
                    Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeErrorType && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpResponseStatusCode]);
                }
            }
            else
            {
                Assert.DoesNotContain(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion);
                Assert.DoesNotContain(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode);

#if NET8_0_OR_GREATER
                // we are using fake address so it will be "name_resolution_error"
                // TODO: test other error types.
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeErrorType && kvp.Value.ToString() == "name_resolution_error");
#elif NETFRAMEWORK
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeErrorType && kvp.Value.ToString() == "name_resolution_failure");

#else
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeErrorType && kvp.Value.ToString() == "System.Net.Http.HttpRequestException");
#endif
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

    private static async Task CheckEnrichment(Sampler sampler, bool enrichExpected, string url)
    {
        bool enrichWithHttpWebRequestCalled = false;
        bool enrichWithHttpWebResponseCalled = false;

        bool enrichWithHttpRequestMessageCalled = false;
        bool enrichWithHttpResponseMessageCalled = false;

        using (Sdk.CreateTracerProviderBuilder()
            .SetSampler(sampler)
            .AddHttpClientInstrumentation(options =>
            {
                options.EnrichWithHttpWebRequest = (activity, httpRequestMessage) => { enrichWithHttpWebRequestCalled = true; };
                options.EnrichWithHttpWebResponse = (activity, httpResponseMessage) => { enrichWithHttpWebResponseCalled = true; };

                options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) => { enrichWithHttpRequestMessageCalled = true; };
                options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) => { enrichWithHttpResponseMessageCalled = true; };
            })
            .Build())
        {
            using var c = new HttpClient();
            using var r = await c.GetAsync(url);
        }

        if (enrichExpected)
        {
#if NETFRAMEWORK
            Assert.True(enrichWithHttpWebRequestCalled);
            Assert.True(enrichWithHttpWebResponseCalled);

            Assert.False(enrichWithHttpRequestMessageCalled);
            Assert.False(enrichWithHttpResponseMessageCalled);
#else
            Assert.False(enrichWithHttpWebRequestCalled);
            Assert.False(enrichWithHttpWebResponseCalled);

            Assert.True(enrichWithHttpRequestMessageCalled);
            Assert.True(enrichWithHttpResponseMessageCalled);
#endif
        }
        else
        {
            Assert.False(enrichWithHttpWebRequestCalled);
            Assert.False(enrichWithHttpWebResponseCalled);

            Assert.False(enrichWithHttpRequestMessageCalled);
            Assert.False(enrichWithHttpResponseMessageCalled);
        }
    }
}