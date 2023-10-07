// <copyright file="HttpClientTests.cs" company="OpenTelemetry Authors">
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
#if NETFRAMEWORK
using System.Net.Http;
#endif
#if !NET8_0
using System.Reflection;
using System.Text.Json;
#endif
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;
using static OpenTelemetry.Internal.HttpSemanticConventionHelper;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public partial class HttpClientTests
{
    public static readonly IEnumerable<object[]> TestData = HttpTestData.ReadTestCases();

#if !NET8_0
    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyTracesAndMetricsOldSemanticConventionsAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: true,
            enableMetrics: true,
            semanticConvention: HttpSemanticConvention.Old).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyTracesAndMetricsNewSemanticConventionsAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: true,
            enableMetrics: true,
            semanticConvention: HttpSemanticConvention.New).ConfigureAwait(false);
    }

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyTracesAndMetricsDuplicateSemanticConventionsAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: true,
            enableMetrics: true,
            semanticConvention: HttpSemanticConvention.Dupe).ConfigureAwait(false);
    }
#endif

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyTracesOnlyAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: true,
            enableMetrics: false).ConfigureAwait(false);
    }

#if !NET8_0
    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyMetricsOnlyAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: false,
            enableMetrics: true).ConfigureAwait(false);
    }
#endif

    [Theory]
    [MemberData(nameof(TestData))]
    public async Task HttpOutCallsAreCollectedSuccessfullyNoSignalsAsync(HttpTestData.HttpOutTestCase tc)
    {
        await HttpOutCallsAreCollectedSuccessfullyBodyAsync(
            this.host,
            this.port,
            tc,
            enableTracing: false,
            enableMetrics: false).ConfigureAwait(false);
    }

#if !NET8_0
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
                    ""spanName"": ""HTTP GET"",
                    ""spanStatus"": ""Unset"",
                    ""spanKind"": ""Client"",
                    ""spanAttributes"": {
                      ""http.scheme"": ""http"",
                      ""http.method"": ""GET"",
                      ""net.peer.name"": ""{host}"",
                      ""net.peer.port"": ""{port}"",
                      ""http.status_code"": ""399"",
                      ""http.flavor"": ""{flavor}"",
                      ""http.url"": ""http://{host}:{port}/""
                    }
                  }
                ]
                ",
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var t = (Task)this.GetType().InvokeMember(nameof(this.HttpOutCallsAreCollectedSuccessfullyTracesAndMetricsOldSemanticConventionsAsync), BindingFlags.InvokeMethod, null, this, HttpTestData.GetArgumentsFromTestCaseObject(input).First());
        await t.ConfigureAwait(false);
    }
#endif

    [Fact]
    public async Task CheckEnrichmentWhenSampling()
    {
        await CheckEnrichment(new AlwaysOffSampler(), false, this.url).ConfigureAwait(false);
        await CheckEnrichment(new AlwaysOnSampler(), true, this.url).ConfigureAwait(false);
    }

#if NET8_0
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
                Version = new Version(2, 0),
            };

            request.Headers.Add("contextRequired", "false");
            request.Headers.Add("responseCode", (tc.ResponseCode == 0 ? 200 : tc.ResponseCode).ToString());
            await c.SendAsync(request).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // test case can intentionally send request that will result in exception
        }
        finally
        {
            meterProvider.Dispose();
        }

        // dns.lookups.duration is a typo
        // https://github.com/dotnet/runtime/issues/92917
        var requestMetrics = metrics
            .Where(metric =>
            metric.Name == "http.client.request.duration" ||
            metric.Name == "http.client.active_requests" ||
            metric.Name == "http.client.request.time_in_queue" ||
            metric.Name == "http.client.connection.duration" ||
            metric.Name == "http.client.open_connections" ||
            metric.Name == "dns.lookups.duration")
            .ToArray();

        if (tc.ResponseExpected)
        {
            Assert.Equal(6, requestMetrics.Count());
        }
        else
        {
            Assert.Equal(4, requestMetrics.Count());
        }

        // Validate http.client.request.duration metric
        var requestDurationMetric = requestMetrics.FirstOrDefault(m => m.Name == "http.client.request.duration");
        this.ValidateRequestDurationMetric(requestDurationMetric, tc);

        // Validate http.client.request.time_in_queue metric
        var requestTimeInQueueMetric = requestMetrics.FirstOrDefault(m => m.Name == "http.client.request.time_in_queue");
        this.ValidateRequestTimeInQueueMetric(requestTimeInQueueMetric, tc);

        // Validate dns.lookups.duration metric
        var dnsLookUpDuration = requestMetrics.FirstOrDefault(m => m.Name == "dns.lookups.duration");
        this.ValidateDnsLookUpDurationMetric(dnsLookUpDuration, tc);

        if (tc.ResponseExpected)
        {
            // Validate http.client.connection.duration metric
            var clientConnectionDuration = requestMetrics.FirstOrDefault(m => m.Name == "http.client.connection.duration");
            this.ValidateHttpClientConnectionDurationMetric(clientConnectionDuration, tc);

            // Validate http.client.open_connections metric
            var clientOpenConnections = requestMetrics.FirstOrDefault(m => m.Name == "http.client.open_connections");
            this.ValidateClientOpenConnections(clientOpenConnections, tc);
        }

        // Validate http.client.active_requests metric
        var clientActiveRequests = requestMetrics.FirstOrDefault(m => m.Name == "http.client.active_requests");
        this.ValidateClientActiveRequests(clientActiveRequests, tc);
    }
#endif

    private static async Task HttpOutCallsAreCollectedSuccessfullyBodyAsync(
        string host,
        int port,
        HttpTestData.HttpOutTestCase tc,
        bool enableTracing,
        bool enableMetrics,
        HttpSemanticConvention? semanticConvention = null)
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
                .AddHttpClientInstrumentation()
                .ConfigureServices(
                    s => s.AddSingleton(BuildConfigurationWithSemanticConventionOptIn(semanticConvention)));
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
                })
                .ConfigureServices(
                    s => s.AddSingleton(BuildConfigurationWithSemanticConventionOptIn(semanticConvention)));
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
#if NETFRAMEWORK
                Version = new Version(1, 1),
#else
                Version = new Version(2, 0),
#endif
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

            await c.SendAsync(request).ConfigureAwait(false);
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
            .Where(metric => metric.Name == "http.client.duration" || metric.Name == "http.client.request.duration")
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

            if (tc.SpanStatusHasDescription.HasValue)
            {
                var desc = activity.StatusDescription;
                Assert.Equal(tc.SpanStatusHasDescription.Value, !string.IsNullOrEmpty(desc));
            }

            var normalizedAttributes = activity.TagObjects.Where(kv => !kv.Key.StartsWith("otel.")).ToDictionary(x => x.Key, x => x.Value.ToString());

            var expectedAttributeCount = semanticConvention == HttpSemanticConvention.Dupe
                ? 11 + (tc.ResponseExpected ? 2 : 0)
                : semanticConvention == HttpSemanticConvention.New
                    ? 5 + (tc.ResponseExpected ? 1 : 0)
                    : 6 + (tc.ResponseExpected ? 1 : 0);

            Assert.Equal(expectedAttributeCount, normalizedAttributes.Count);

            if (semanticConvention == null || semanticConvention.Value.HasFlag(HttpSemanticConvention.Old))
            {
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpMethod]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeNetPeerName && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeNetPeerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpScheme && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpScheme]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpUrl && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpUrl]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpFlavor && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpFlavor]);
                if (tc.ResponseExpected)
                {
                    Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpStatusCode && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpStatusCode]);
                }
                else
                {
                    Assert.DoesNotContain(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpStatusCode);
                }
            }

            if (semanticConvention != null && semanticConvention.Value.HasFlag(HttpSemanticConvention.New))
            {
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpMethod]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeUrlFull && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpUrl]);
                Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpFlavor]);
                if (tc.ResponseExpected)
                {
                    Assert.Contains(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpStatusCode]);
                }
                else
                {
                    Assert.DoesNotContain(normalizedAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode);
                }
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
            if (semanticConvention == HttpSemanticConvention.Dupe)
            {
                Assert.Equal(2, requestMetrics.Length);
            }
            else
            {
                Assert.Single(requestMetrics);
            }

#if !NET8_0
            if (semanticConvention == null || semanticConvention.Value.HasFlag(HttpSemanticConvention.Old))
            {
                var metric = requestMetrics.FirstOrDefault(m => m.Name == "http.client.duration");
                Assert.NotNull(metric);
                Assert.Equal("ms", metric.Unit);
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
                    Assert.Equal(activity.Duration.TotalMilliseconds, sum);
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

                var expectedAttributeCount = 5 + (tc.ResponseExpected ? 1 : 0);

                Assert.Equal(expectedAttributeCount, attributes.Count);

                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpMethod]);
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetPeerName && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetPeerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpScheme && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpScheme]);
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpFlavor && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpFlavor]);
                if (tc.ResponseExpected)
                {
                    Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpStatusCode && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpStatusCode]);
                }
                else
                {
                    Assert.DoesNotContain(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpStatusCode);
                }

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
            }
#endif
            if (semanticConvention != null && semanticConvention.Value.HasFlag(HttpSemanticConvention.New))
            {
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
                    Assert.Equal(activity.Duration.TotalSeconds, sum);
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

                var expectedAttributeCount = 4 + (tc.ResponseExpected ? 1 : 0);

                Assert.Equal(expectedAttributeCount, attributes.Count);

                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpMethod]);
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);
                Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpFlavor]);
                if (tc.ResponseExpected)
                {
                    Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpStatusCode]);
                }
                else
                {
                    Assert.DoesNotContain(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode);
                }

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
            }
        }
    }

    private static IConfiguration BuildConfigurationWithSemanticConventionOptIn(
        HttpSemanticConvention? semanticConvention)
    {
        var builder = new ConfigurationBuilder();

        if (semanticConvention != null && semanticConvention != HttpSemanticConvention.Old)
        {
            builder.AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    ["OTEL_SEMCONV_STABILITY_OPT_IN"] = semanticConvention == HttpSemanticConvention.Dupe
                        ? "http/dup"
                        : "http",
                });
        }

        return builder.Build();
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
            using var r = await c.GetAsync(url).ConfigureAwait(false);
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

#if NET8_0
    private void ValidateClientActiveRequests(Metric clientActiveRequests, HttpTestData.HttpOutTestCase tc)
    {
        var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, this.host, this.port));

        Assert.NotNull(clientActiveRequests);
        Assert.Equal("{request}", clientActiveRequests.Unit);
        Assert.True(clientActiveRequests.MetricType == MetricType.LongSumNonMonotonic);

        var clientActiveRequestsMetricPoints = new List<MetricPoint>();
        foreach (var p in clientActiveRequests.GetMetricPoints())
        {
            clientActiveRequestsMetricPoints.Add(p);
        }

        Assert.Single(clientActiveRequestsMetricPoints);
        var clientActiveRequestsMetricPoint = clientActiveRequestsMetricPoints[0];

        var clientActiveRequestsAttributes = new Dictionary<string, object>();
        foreach (var tag in clientActiveRequestsMetricPoint.Tags)
        {
            clientActiveRequestsAttributes[tag.Key] = tag.Value;
        }

        Assert.Equal(4, clientActiveRequestsAttributes.Count());

        Assert.Contains(clientActiveRequestsAttributes, kvp => kvp.Key == SemanticConventions.AttributeUrlScheme && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpScheme]);
        Assert.Contains(clientActiveRequestsAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpMethod]);
        Assert.Contains(clientActiveRequestsAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
        Assert.Contains(clientActiveRequestsAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);
    }

    private void ValidateClientOpenConnections(Metric clientOpenConnections, HttpTestData.HttpOutTestCase tc)
    {
        var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, this.host, this.port));

        Assert.NotNull(clientOpenConnections);
        Assert.Equal("{connection}", clientOpenConnections.Unit);
        Assert.True(clientOpenConnections.MetricType == MetricType.LongSumNonMonotonic);

        var clientOpenConnectionsMetricPoints = new List<MetricPoint>();
        foreach (var p in clientOpenConnections.GetMetricPoints())
        {
            clientOpenConnectionsMetricPoints.Add(p);
        }

        // Two metricPoints
        // one for active connections
        // one for idle connetions
        Assert.Equal(2, clientOpenConnectionsMetricPoints.Count());

        clientOpenConnectionsMetricPoints.ForEach((clientOpenConnectionsMetricPoint) =>
        {
            var clientOpenConnectionsAttributes = new Dictionary<string, object>();
            foreach (var tag in clientOpenConnectionsMetricPoint.Tags)
            {
                clientOpenConnectionsAttributes[tag.Key] = tag.Value;
            }

            Assert.Equal(6, clientOpenConnectionsAttributes.Count());

            Assert.Contains(clientOpenConnectionsAttributes, kvp => kvp.Key == "http.connection.state" && (kvp.Value.ToString() == "idle" || kvp.Value.ToString() == "active"));
            Assert.Contains(clientOpenConnectionsAttributes, kvp => kvp.Key == SemanticConventions.AttributeUrlScheme && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpScheme]);
            Assert.Contains(clientOpenConnectionsAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
            Assert.Contains(clientOpenConnectionsAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);
            Assert.Contains(clientOpenConnectionsAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerSocketAddress && kvp.Value.ToString() == "::1");

            // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/4928
            // Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpFlavor]);
        });
    }

    private void ValidateHttpClientConnectionDurationMetric(Metric clientConnectionDuration, HttpTestData.HttpOutTestCase tc)
    {
        var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, this.host, this.port));

        Assert.NotNull(clientConnectionDuration);
        Assert.Equal("s", clientConnectionDuration.Unit);
        Assert.True(clientConnectionDuration.MetricType == MetricType.Histogram);

        var clientConnectionDurationMetricMetricPoints = new List<MetricPoint>();
        foreach (var p in clientConnectionDuration.GetMetricPoints())
        {
            clientConnectionDurationMetricMetricPoints.Add(p);
        }

        Assert.Single(clientConnectionDurationMetricMetricPoints);
        var clientConnectionDurationMetricPoint = clientConnectionDurationMetricMetricPoints[0];

        var count = clientConnectionDurationMetricPoint.GetHistogramCount();
        var sum = clientConnectionDurationMetricPoint.GetHistogramSum();

        Assert.Equal(1L, count);

        // Inspect Metric Attributes
        var clientConnectionDurationAttributes = new Dictionary<string, object>();
        foreach (var tag in clientConnectionDurationMetricPoint.Tags)
        {
            clientConnectionDurationAttributes[tag.Key] = tag.Value;
        }

        Assert.Equal(5, clientConnectionDurationAttributes.Count());

        Assert.Contains(clientConnectionDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeUrlScheme && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpScheme]);
        Assert.Contains(clientConnectionDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
        Assert.Contains(clientConnectionDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);
        Assert.Contains(clientConnectionDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerSocketAddress && kvp.Value.ToString() == "::1");

        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/4928
        // Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpFlavor]);
    }

    private void ValidateDnsLookUpDurationMetric(Metric dnsLookUpDurationMetric, HttpTestData.HttpOutTestCase tc)
    {
        var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, this.host, this.port));

        Assert.NotNull(dnsLookUpDurationMetric);
        Assert.Equal("s", dnsLookUpDurationMetric.Unit);
        Assert.True(dnsLookUpDurationMetric.MetricType == MetricType.Histogram);

        var dnsLookUpDurationMetricMetricPoints = new List<MetricPoint>();
        foreach (var p in dnsLookUpDurationMetric.GetMetricPoints())
        {
            dnsLookUpDurationMetricMetricPoints.Add(p);
        }

        Assert.Single(dnsLookUpDurationMetricMetricPoints);
        var dnsLookUpDurationMetricMetricPoint = dnsLookUpDurationMetricMetricPoints[0];

        var count = dnsLookUpDurationMetricMetricPoint.GetHistogramCount();
        var sum = dnsLookUpDurationMetricMetricPoint.GetHistogramSum();

        Assert.Equal(1L, count);

        // Inspect Metric Attributes
        var dnsLookUpDurationAttributes = new Dictionary<string, object>();
        foreach (var tag in dnsLookUpDurationMetricMetricPoint.Tags)
        {
            dnsLookUpDurationAttributes[tag.Key] = tag.Value;
        }

        Assert.Single(dnsLookUpDurationAttributes);

        Assert.Contains(dnsLookUpDurationAttributes, kvp => kvp.Key == "dns.question.name" && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
    }

    private void ValidateRequestTimeInQueueMetric(Metric requestTimeInQueueMetric, HttpTestData.HttpOutTestCase tc)
    {
        var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, this.host, this.port));

        Assert.NotNull(requestTimeInQueueMetric);
        Assert.Equal("s", requestTimeInQueueMetric.Unit);
        Assert.True(requestTimeInQueueMetric.MetricType == MetricType.Histogram);

        var requestTimeInQueueMetricPoints = new List<MetricPoint>();
        foreach (var p in requestTimeInQueueMetric.GetMetricPoints())
        {
            requestTimeInQueueMetricPoints.Add(p);
        }

        Assert.Single(requestTimeInQueueMetricPoints);
        var requestTimeInQueueMetricPoint = requestTimeInQueueMetricPoints[0];

        var count = requestTimeInQueueMetricPoint.GetHistogramCount();
        var sum = requestTimeInQueueMetricPoint.GetHistogramSum();

        Assert.Equal(1L, count);

        // Inspect Metric Attributes
        var requestTimeInQueueAttributes = new Dictionary<string, object>();
        foreach (var tag in requestTimeInQueueMetricPoint.Tags)
        {
            requestTimeInQueueAttributes[tag.Key] = tag.Value;
        }

        Assert.Equal(5, requestTimeInQueueAttributes.Count());

        Assert.Contains(requestTimeInQueueAttributes, kvp => kvp.Key == SemanticConventions.AttributeUrlScheme && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpScheme]);
        Assert.Contains(requestTimeInQueueAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpMethod]);
        Assert.Contains(requestTimeInQueueAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
        Assert.Contains(requestTimeInQueueAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);

        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/4928
        // Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpFlavor]);
    }

    private void ValidateRequestDurationMetric(Metric requestDurationMetric, HttpTestData.HttpOutTestCase tc)
    {
        var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, this.host, this.port));

        Assert.NotNull(requestDurationMetric);
        Assert.Equal("s", requestDurationMetric.Unit);
        Assert.True(requestDurationMetric.MetricType == MetricType.Histogram);

        var requestDurationMetricPoints = new List<MetricPoint>();
        foreach (var p in requestDurationMetric.GetMetricPoints())
        {
            requestDurationMetricPoints.Add(p);
        }

        Assert.Single(requestDurationMetricPoints);
        var requestDurationMetricPoint = requestDurationMetricPoints[0];

        var count = requestDurationMetricPoint.GetHistogramCount();
        var sum = requestDurationMetricPoint.GetHistogramSum();

        Assert.Equal(1L, count);

        // Inspect Metric Attributes
        var requestDurationAttributes = new Dictionary<string, object>();
        foreach (var tag in requestDurationMetricPoint.Tags)
        {
            requestDurationAttributes[tag.Key] = tag.Value;
        }

        var expectedRequestDurationAttributeCount = 5 + (tc.ResponseExpected ? 1 : 0);

        Assert.Equal(expectedRequestDurationAttributeCount, requestDurationAttributes.Count);

        Assert.Contains(requestDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeUrlScheme && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpScheme]);
        Assert.Contains(requestDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpMethod]);
        Assert.Contains(requestDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerAddress && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerName]);
        Assert.Contains(requestDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeServerPort && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeNetPeerPort]);

        // See https://github.com/open-telemetry/opentelemetry-dotnet/issues/4928
        // Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeNetworkProtocolVersion && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpFlavor]);
        if (tc.ResponseExpected)
        {
            Assert.Contains(requestDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode && kvp.Value.ToString() == normalizedAttributesTestCase[SemanticConventions.AttributeHttpStatusCode]);
        }
        else
        {
            Assert.DoesNotContain(requestDurationAttributes, kvp => kvp.Key == SemanticConventions.AttributeHttpResponseStatusCode);
        }

        // Inspect Histogram Bounds
        var histogramBuckets = requestDurationMetricPoint.GetHistogramBuckets();
        var histogramBounds = new List<double>();
        foreach (var t in histogramBuckets)
        {
            histogramBounds.Add(t.ExplicitBound);
        }

        Assert.Equal(
            expected: new List<double> { 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10, double.PositiveInfinity },
            actual: histogramBounds);
    }
#endif
}
