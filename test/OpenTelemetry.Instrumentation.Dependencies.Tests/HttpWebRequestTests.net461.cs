// <copyright file="HttpWebRequestTests.net461.cs" company="OpenTelemetry Authors">
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
#if NET461
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Moq;
using Newtonsoft.Json;
using OpenTelemetry.Internal.Test;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Instrumentation.Dependencies.Tests
{
    public partial class HttpWebRequestTests
    {
        public static IEnumerable<object[]> TestData => HttpTestData.ReadTestCases();

        [Theory]
        [MemberData(nameof(TestData))]
        public void HttpOutCallsAreCollectedSuccessfullyAsync(HttpTestData.HttpOutTestCase tc)
        {
            using var serverLifeTime = TestHttpServer.RunServer(
                (ctx) =>
                {
                    ctx.Response.StatusCode = tc.ResponseCode == 0 ? 200 : tc.ResponseCode;
                    ctx.Response.OutputStream.Close();
                },
                out var host,
                out var port);

            var activityProcessor = new Mock<ActivityProcessor>();
            using var shutdownSignal = OpenTelemetrySdk.EnableOpenTelemetry(b =>
            {
                b.SetProcessorPipeline(c => c.AddProcessor(ap => activityProcessor.Object));
                b.AddHttpWebRequestDependencyInstrumentation();
            });

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

                new StreamReader(response.GetResponseStream()).ReadToEnd();
            }
            catch (Exception)
            {
                //test case can intentionally send request that will result in exception
            }

            Assert.Equal(2, activityProcessor.Invocations.Count); // begin and end was called
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal(tc.SpanName, activity.DisplayName);
            Assert.Equal(tc.SpanKind, activity.Kind.ToString());

            var d = new Dictionary<string, string>()
            {
                { StatusCanonicalCode.Ok.ToString(), "OK"},
                { StatusCanonicalCode.Cancelled.ToString(), "CANCELLED"},
                { StatusCanonicalCode.Unknown.ToString(), "UNKNOWN"},
                { StatusCanonicalCode.InvalidArgument.ToString(), "INVALID_ARGUMENT"},
                { StatusCanonicalCode.DeadlineExceeded.ToString(), "DEADLINE_EXCEEDED"},
                { StatusCanonicalCode.NotFound.ToString(), "NOT_FOUND"},
                { StatusCanonicalCode.AlreadyExists.ToString(), "ALREADY_EXISTS"},
                { StatusCanonicalCode.PermissionDenied.ToString(), "PERMISSION_DENIED"},
                { StatusCanonicalCode.ResourceExhausted.ToString(), "RESOURCE_EXHAUSTED"},
                { StatusCanonicalCode.FailedPrecondition.ToString(), "FAILED_PRECONDITION"},
                { StatusCanonicalCode.Aborted.ToString(), "ABORTED"},
                { StatusCanonicalCode.OutOfRange.ToString(), "OUT_OF_RANGE"},
                { StatusCanonicalCode.Unimplemented.ToString(), "UNIMPLEMENTED"},
                { StatusCanonicalCode.Internal.ToString(), "INTERNAL"},
                { StatusCanonicalCode.Unavailable.ToString(), "UNAVAILABLE"},
                { StatusCanonicalCode.DataLoss.ToString(), "DATA_LOSS"},
                { StatusCanonicalCode.Unauthenticated.ToString(), "UNAUTHENTICATED"},
            };

            tc.SpanAttributes = tc.SpanAttributes.ToDictionary(
                x => x.Key,
                x =>
                {
                    if (x.Key == "http.flavor" && x.Value == "2.0")
                        return "1.1";
                    return HttpTestData.NormalizeValues(x.Value, host, port);
                });

            foreach (KeyValuePair<string, string> tag in activity.Tags)
            {
                if (!tc.SpanAttributes.TryGetValue(tag.Key, out string value))
                {
                    if (tag.Key == "http.flavor")
                    {
                        // http.flavor is optional in .NET Core instrumentation but there is no way to pass that option to the new ActivitySource model so it always shows up here.
                        if (tc.SetHttpFlavor)
                        {
                            Assert.Equal(value, tag.Value);
                        }
                        continue;
                    }
                    if (tag.Key == SpanAttributeConstants.StatusCodeKey)
                    {
                        Assert.Equal(tc.SpanStatus, d[tag.Value]);
                        continue;
                    }
                    if (tag.Key == SpanAttributeConstants.StatusDescriptionKey)
                    {
                        if (tc.SpanStatusHasDescription.HasValue)
                            Assert.Equal(tc.SpanStatusHasDescription.Value, !string.IsNullOrEmpty(tag.Value));
                        continue;
                    }

                    Assert.True(false, $"Tag {tag.Key} was not found in test data.");
                }
                Assert.Equal(value, tag.Value);
            }
        }

        [Fact]
        public void DebugIndividualTestAsync()
        {
            var serializer = new JsonSerializer();
            var input = serializer.Deserialize<HttpTestData.HttpOutTestCase>(new JsonTextReader(new StringReader(@"
  {
    ""name"": ""Http version attribute populated"",
    ""method"": ""GET"",
    ""url"": ""http://{host}:{port}/"",
    ""responseCode"": 200,
    ""spanName"": ""HTTP GET"",
    ""spanStatus"": ""OK"",
    ""spanKind"": ""Client"",
    ""setHttpFlavor"": true,
    ""spanAttributes"": {
      ""component"": ""http"",
      ""http.method"": ""GET"",
      ""http.host"": ""{host}:{port}"",
      ""http.flavor"": ""2.0"",
      ""http.status_code"": ""200"",
      ""http.url"": ""http://{host}:{port}/""
    }
  }
")));
            this.HttpOutCallsAreCollectedSuccessfullyAsync(input);
        }
    }
}
#endif
