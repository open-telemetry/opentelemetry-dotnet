// <copyright file="HttpWebRequestTests.netfx.cs" company="OpenTelemetry Authors">
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
#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Moq;
using Newtonsoft.Json;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests
{
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

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddHttpClientInstrumentation(options =>
                {
                    options.Enrich = ActivityEnrichment;
                    options.RecordException = tc.RecordException.HasValue ? tc.RecordException.Value : false;
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

                new StreamReader(response.GetResponseStream()).ReadToEnd();
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
                    if (x.Key == "http.flavor" && x.Value == "2.0")
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
                        if (tc.SpanStatusHasDescription.HasValue)
                        {
                            Assert.Equal(tc.SpanStatusHasDescription.Value, !string.IsNullOrEmpty(tagValue));
                        }

                        continue;
                    }

                    Assert.True(false, $"Tag {tag.Key} was not found in test data.");
                }

                Assert.Equal(value, tagValue);
            }

            if (tc.RecordException.HasValue && tc.RecordException.Value)
            {
                Assert.Single(activity.Events.Where(evt => evt.Name.Equals("exception")));
            }
        }

        [Fact]
        public void DebugIndividualTest()
        {
            var serializer = new JsonSerializer();
            var input = serializer.Deserialize<HttpTestData.HttpOutTestCase>(new JsonTextReader(new StringReader(@"
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
      ""http.method"": ""GET"",
      ""http.host"": ""{host}:{port}"",
      ""http.flavor"": ""2.0"",
      ""http.status_code"": 200,
      ""http.url"": ""http://{host}:{port}/""
    }
  }
")));
            this.HttpOutCallsAreCollectedSuccessfully(input);
        }

        private static void ValidateHttpWebRequestActivity(Activity activityToValidate)
        {
            Assert.Equal(ActivityKind.Client, activityToValidate.Kind);
        }

        private static void ActivityEnrichment(Activity activity, string method, object obj)
        {
            switch (method)
            {
                case "OnStartActivity":
                    Assert.True(obj is HttpWebRequest);
                    break;

                case "OnStopActivity":
                    Assert.True(obj is HttpWebResponse);
                    break;

                case "OnException":
                    Assert.True(obj is Exception);
                    break;

                default:
                    break;
            }
        }
    }
}
#endif
