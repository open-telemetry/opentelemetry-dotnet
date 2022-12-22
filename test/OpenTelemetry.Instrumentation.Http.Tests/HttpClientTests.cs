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
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Moq;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests
{
    public partial class HttpClientTests
    {
        public static IEnumerable<object[]> TestData => HttpTestData.ReadTestCases();

        [Theory]
        [MemberData(nameof(TestData))]
        public async Task HttpOutCallsAreCollectedSuccessfullyAsync(HttpTestData.HttpOutTestCase tc)
        {
            bool enrichWithHttpWebRequestCalled = false;
            bool enrichWithHttpWebResponseCalled = false;
            bool enrichWithHttpRequestMessageCalled = false;
            bool enrichWithHttpResponseMessageCalled = false;
            bool enrichWithExceptionCalled = false;

            using var serverLifeTime = TestHttpServer.RunServer(
                (ctx) =>
                {
                    ctx.Response.StatusCode = tc.ResponseCode == 0 ? 200 : tc.ResponseCode;
                    ctx.Response.OutputStream.Close();
                },
                out var host,
                out var port);

            var processor = new Mock<BaseProcessor<Activity>>();
            tc.Url = HttpTestData.NormalizeValues(tc.Url, host, port);

            var metrics = new List<Metric>();

            var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(metrics)
                .Build();

            using (Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation((opt) =>
                {
                    opt.EnrichWithHttpWebRequest = (activity, httpRequestMessage) => { enrichWithHttpWebRequestCalled = true; };
                    opt.EnrichWithHttpWebResponse = (activity, httpResponseMessage) => { enrichWithHttpWebResponseCalled = true; };
                    opt.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) => { enrichWithHttpRequestMessageCalled = true; };
                    opt.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) => { enrichWithHttpResponseMessageCalled = true; };
                    opt.EnrichWithException = (activity, exception) => { enrichWithExceptionCalled = true; };
                    opt.RecordException = tc.RecordException ?? false;
                })
                .AddProcessor(processor.Object)
                .Build())
            {
                try
                {
                    using var c = new HttpClient();
                    using var request = new HttpRequestMessage
                    {
                        RequestUri = new Uri(tc.Url),
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

                    await c.SendAsync(request).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // test case can intentionally send request that will result in exception
                }
            }

            meterProvider.Dispose();

            var requestMetrics = metrics
                .Where(metric => metric.Name == "http.client.duration")
                .ToArray();

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.
            var activity = (Activity)processor.Invocations[2].Arguments[0];

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
            var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, host, port));

            Assert.Equal(normalizedAttributesTestCase.Count, normalizedAttributes.Count);

            foreach (var kv in normalizedAttributesTestCase)
            {
                Assert.Contains(activity.TagObjects, i => i.Key == kv.Key && i.Value.ToString().Equals(kv.Value, StringComparison.OrdinalIgnoreCase));
            }

            if (tc.RecordException.HasValue && tc.RecordException.Value)
            {
                Assert.Single(activity.Events.Where(evt => evt.Name.Equals("exception")));
                Assert.True(enrichWithExceptionCalled);
            }

            if (tc.ResponseExpected)
            {
#if NETFRAMEWORK
                Assert.Empty(requestMetrics);
#else
                Assert.Single(requestMetrics);

                var metric = requestMetrics[0];
                Assert.NotNull(metric);
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
                Assert.Equal(activity.Duration.TotalMilliseconds, sum);

                var attributes = new KeyValuePair<string, object>[metricPoint.Tags.Count];
                int i = 0;
                foreach (var tag in metricPoint.Tags)
                {
                    attributes[i++] = tag;
                }

                var method = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, tc.Method);
                var scheme = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, "http");
                var statusCode = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, tc.ResponseCode == 0 ? 200 : tc.ResponseCode);
                var flavor = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, "2.0");
                var hostName = new KeyValuePair<string, object>(SemanticConventions.AttributeNetPeerName, host);
                var portNumber = new KeyValuePair<string, object>(SemanticConventions.AttributeNetPeerPort, port);
                Assert.Contains(method, attributes);
                Assert.Contains(scheme, attributes);
                Assert.Contains(statusCode, attributes);
                Assert.Contains(flavor, attributes);
                Assert.Contains(hostName, attributes);
                Assert.Contains(portNumber, attributes);
                Assert.Equal(6, attributes.Length);
#endif
            }
            else
            {
                Assert.Empty(requestMetrics);
            }
        }

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

            var t = (Task)this.GetType().InvokeMember(nameof(this.HttpOutCallsAreCollectedSuccessfullyAsync), BindingFlags.InvokeMethod, null, this, HttpTestData.GetArgumentsFromTestCaseObject(input).First());
            await t.ConfigureAwait(false);
        }

        [Fact]
        public async Task CheckEnrichmentWhenSampling()
        {
            await CheckEnrichment(new AlwaysOffSampler(), false, this.url).ConfigureAwait(false);
            await CheckEnrichment(new AlwaysOnSampler(), true, this.url).ConfigureAwait(false);
        }

        private static async Task CheckEnrichment(Sampler sampler, bool enrichExpected, string url)
        {
            bool enrichWithHttpWebRequestCalled = false;
            bool enrichWithHttpWebResponseCalled = false;

            bool enrichWithHttpRequestMessageCalled = false;
            bool enrichWithHttpResponseMessageCalled = false;

            var processor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .SetSampler(sampler)
                .AddHttpClientInstrumentation(options =>
                {
                    options.EnrichWithHttpWebRequest = (activity, httpRequestMessage) => { enrichWithHttpWebRequestCalled = true; };
                    options.EnrichWithHttpWebResponse = (activity, httpResponseMessage) => { enrichWithHttpWebResponseCalled = true; };

                    options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) => { enrichWithHttpRequestMessageCalled = true; };
                    options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) => { enrichWithHttpResponseMessageCalled = true; };
                })
                .AddProcessor(processor.Object)
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
    }
}
