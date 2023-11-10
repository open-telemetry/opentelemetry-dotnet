// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Moq;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

#pragma warning disable SYSLIB0014 // Type or member is obsolete

namespace OpenTelemetry.Instrumentation.Http.Tests;

public partial class HttpWebRequestTests
{
    public static IEnumerable<object[]> TestData => HttpTestData.ReadTestCases();

    [Theory]
    [MemberData(nameof(TestData))]
    public void HttpOutCallsAreCollectedSuccessfully(HttpTestData.HttpOutTestCase tc)
    {
        using var serverLifeTime = TestHttpServer.RunServer(
            (ctx) =>
            {
                ctx.Response.StatusCode = tc.ResponseCode == 0 ? 200 : tc.ResponseCode;
                ctx.Response.OutputStream.Close();
            },
            out var host,
            out var port);

        bool enrichWithHttpWebRequestCalled = false;
        bool enrichWithHttpWebResponseCalled = false;
        bool enrichWithHttpRequestMessageCalled = false;
        bool enrichWithHttpResponseMessageCalled = false;
        bool enrichWithExceptionCalled = false;

        var activityProcessor = new Mock<BaseProcessor<Activity>>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(activityProcessor.Object)
            .AddHttpClientInstrumentation(options =>
            {
                options.EnrichWithHttpWebRequest = (activity, httpWebRequest) => { enrichWithHttpWebRequestCalled = true; };
                options.EnrichWithHttpWebResponse = (activity, httpWebResponse) => { enrichWithHttpWebResponseCalled = true; };
                options.EnrichWithHttpRequestMessage = (activity, request) => { enrichWithHttpRequestMessageCalled = true; };
                options.EnrichWithHttpResponseMessage = (activity, response) => { enrichWithHttpResponseMessageCalled = true; };
                options.EnrichWithException = (activity, exception) => { enrichWithExceptionCalled = true; };
                options.RecordException = tc.RecordException ?? false;
            })
            .Build();

        tc.Url = HttpTestData.NormalizeValues(tc.Url, host, port);

        try
        {
            var request = (HttpWebRequest)WebRequest.Create(tc.Url);

            request.Method = tc.Method;

            if (tc.Headers != null)
            {
                foreach (var header in tc.Headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            request.ContentLength = 0;

            using var response = (HttpWebResponse)request.GetResponse();

            using var streamReader = new StreamReader(response.GetResponseStream());
            streamReader.ReadToEnd();
        }
        catch (Exception)
        {
            // test case can intentionally send request that will result in exception
            tc.ResponseExpected = false;
        }

        Assert.Equal(3, activityProcessor.Invocations.Count); // SetParentProvider/Begin/End called
        var activity = (Activity)activityProcessor.Invocations[2].Arguments[0];
        ValidateHttpWebRequestActivity(activity);
        Assert.Equal(tc.SpanName, activity.DisplayName);

        tc.SpanAttributes = tc.SpanAttributes.ToDictionary(
            x => x.Key,
            x =>
            {
                if (x.Key == "http.flavor")
                {
                    return "1.1";
                }

                return HttpTestData.NormalizeValues(x.Value, host, port);
            });

        foreach (KeyValuePair<string, object> tag in activity.TagObjects)
        {
            var tagValue = tag.Value.ToString();

            if (!tc.SpanAttributes.TryGetValue(tag.Key, out string value))
            {
                if (tag.Key == SpanAttributeConstants.StatusCodeKey)
                {
                    Assert.Equal(tc.SpanStatus, tagValue);
                    continue;
                }

                if (tag.Key == SpanAttributeConstants.StatusDescriptionKey)
                {
                    Assert.Null(tagValue);
                    continue;
                }

                Assert.Fail($"Tag {tag.Key} was not found in test data.");
            }

            Assert.Equal(value, tagValue);
        }

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

        if (tc.RecordException.HasValue && tc.RecordException.Value)
        {
            Assert.Single(activity.Events.Where(evt => evt.Name.Equals("exception")));
            Assert.True(enrichWithExceptionCalled);
        }
    }

    [Fact]
    public void DebugIndividualTest()
    {
        var input = JsonSerializer.Deserialize<HttpTestData.HttpOutTestCase>(
        @"
             {
                ""name"": ""Http version attribute populated"",
                ""method"": ""GET"",
                ""url"": ""http://{host}:{port}/"",
                ""responseCode"": 200,
                ""spanName"": ""HTTP GET"",
                ""spanStatus"": ""UNSET"",
                ""spanKind"": ""Client"",
                ""setHttpFlavor"": true,
                ""spanAttributes"": {
                  ""http.scheme"": ""http"",
                  ""http.method"": ""GET"",
                  ""net.peer.name"": ""{host}"",
                  ""net.peer.port"": ""{port}"",
                  ""http.flavor"": ""1.1"",
                  ""http.status_code"": ""200"",
                  ""http.url"": ""http://{host}:{port}/""
                }
              }
            ",
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        this.HttpOutCallsAreCollectedSuccessfully(input);
    }

    private static void ValidateHttpWebRequestActivity(Activity activityToValidate)
    {
        Assert.Equal(ActivityKind.Client, activityToValidate.Kind);
    }
}
