// <copyright file="HttpWebRequestTests.cs" company="OpenTelemetry Authors">
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
                if (x.Key == "network.protocol.version")
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

                if (tag.Key == SemanticConventions.AttributeErrorType)
                {
                    // TODO: Add validation for error.type in test cases.
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
                ""spanName"": ""GET"",
                ""spanStatus"": ""UNSET"",
                ""spanKind"": ""Client"",
                ""setHttpFlavor"": true,
                ""spanAttributes"": {
                  ""url.scheme"": ""http"",
                  ""http.request.method"": ""GET"",
                  ""server.address"": ""{host}"",
                  ""server.port"": ""{port}"",
                  ""network.protocol.version"": ""1.1"",
                  ""http.response.status_code"": ""200"",
                  ""url.full"": ""http://{host}:{port}/""
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
