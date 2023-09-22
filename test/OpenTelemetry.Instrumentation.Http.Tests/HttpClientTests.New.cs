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
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public partial class HttpClientTests
{
    [Theory]
    [MemberData(nameof(TestData_New))]
    public async Task HttpOutCallsAreCollectedSuccessfullyAsync_New(HttpTestData.HttpOutTestCase tc)
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

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [SemanticConventionOptInKeyName] = "http" })
            .Build();

        var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .AddHttpClientInstrumentation()
            .AddInMemoryExporter(metrics)
            .Build();

        using (Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
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

        this.TestOutputHelper.WriteLine(tc.Name);
        foreach (var kvp in normalizedAttributes) // THIS HELPS DEBUG FAILURES
        {
            this.TestOutputHelper.WriteLine($"\"{kvp.Key}\": \"{kvp.Value}\",");
        }

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

        var requestMetrics = metrics
            .Where(metric => metric.Name == "http.client.request.duration")
            .ToArray();

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
        Assert.Equal(activity.Duration.TotalSeconds, sum);

        var attributes = new KeyValuePair<string, object>[metricPoint.Tags.Count];
        int i = 0;
        foreach (var tag in metricPoint.Tags)
        {
            attributes[i++] = tag;
        }

        var method = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpRequestMethod, tc.Method);
        var protocolVersion = new KeyValuePair<string, object>(SemanticConventions.AttributeNetworkProtocolVersion, "2.0");
        var portNumber = new KeyValuePair<string, object>(SemanticConventions.AttributeServerPort, port);
        var serverAddress = new KeyValuePair<string, object>(SemanticConventions.AttributeServerAddress, tc.ResponseExpected ? host : "sdlfaldfjalkdfjlkajdflkajlsdjf"); // TODO
        // var urlFull = new KeyValuePair<string, object>(SemanticConventions.AttributeUrlFull, tc.Url); // TODO

        var statusCode = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpResponseStatusCode, tc.ResponseCode == 0 ? 200 : tc.ResponseCode);

        Assert.Contains(method, attributes);
        Assert.Contains(protocolVersion, attributes);
        Assert.Contains(portNumber, attributes);
        Assert.Contains(serverAddress, attributes);
        // Assert.Contains(urlFull, attributes);

        //var scheme = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, "http");
        //Assert.Contains(scheme, attributes);

        //var hostName = new KeyValuePair<string, object>(SemanticConventions.AttributeNetPeerName, tc.ResponseExpected ? host : "sdlfaldfjalkdfjlkajdflkajlsdjf");

        //Assert.Contains(hostName, attributes);

        if (tc.ResponseExpected)
        {
            Assert.Contains(statusCode, attributes);
            Assert.Equal(5, attributes.Length);
        }
        else
        {
            Assert.DoesNotContain(statusCode, attributes);
            Assert.Equal(4, attributes.Length); // TODO: IS THIS CORRECT?
        }
#endif
    }

    [Fact]
    public async Task DebugIndividualTestAsync_New()
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
                      ""http.request.method"": ""GET"",
                      ""server.address"": ""localhost"",
                      ""server.port"": ""{port}"",
                      ""url.full"": ""http://{host}:{port}/"",
                      ""network.protocol.version"": ""{flavor}"",
                      ""http.response.status_code"": ""399""
                    }
                  }
                ]
                ",
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var t = (Task)this.GetType().InvokeMember(nameof(this.HttpOutCallsAreCollectedSuccessfullyAsync_New), BindingFlags.InvokeMethod, null, this, HttpTestData.GetArgumentsFromTestCaseObject(input).First());
        await t.ConfigureAwait(false);
    }
}
