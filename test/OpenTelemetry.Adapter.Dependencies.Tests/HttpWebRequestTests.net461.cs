// <copyright file="HttpWebRequestTests.net461.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Adapter.Dependencies.Tests
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

            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);
            tc.Url = HttpTestData.NormalizeValues(tc.Url, host, port);

            using (new HttpWebRequestAdapter(tracer, new HttpClientAdapterOptions() { SetHttpFlavor = tc.SetHttpFlavor }))
            {
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
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(tc.SpanName, span.Name);
            Assert.Equal(tc.SpanKind, span.Kind.ToString());

            var d = new Dictionary<StatusCanonicalCode, string>()
            {
                { StatusCanonicalCode.Ok, "OK"},
                { StatusCanonicalCode.Cancelled, "CANCELLED"},
                { StatusCanonicalCode.Unknown, "UNKNOWN"},
                { StatusCanonicalCode.InvalidArgument, "INVALID_ARGUMENT"},
                { StatusCanonicalCode.DeadlineExceeded, "DEADLINE_EXCEEDED"},
                { StatusCanonicalCode.NotFound, "NOT_FOUND"},
                { StatusCanonicalCode.AlreadyExists, "ALREADY_EXISTS"},
                { StatusCanonicalCode.PermissionDenied, "PERMISSION_DENIED"},
                { StatusCanonicalCode.ResourceExhausted, "RESOURCE_EXHAUSTED"},
                { StatusCanonicalCode.FailedPrecondition, "FAILED_PRECONDITION"},
                { StatusCanonicalCode.Aborted, "ABORTED"},
                { StatusCanonicalCode.OutOfRange, "OUT_OF_RANGE"},
                { StatusCanonicalCode.Unimplemented, "UNIMPLEMENTED"},
                { StatusCanonicalCode.Internal, "INTERNAL"},
                { StatusCanonicalCode.Unavailable, "UNAVAILABLE"},
                { StatusCanonicalCode.DataLoss, "DATA_LOSS"},
                { StatusCanonicalCode.Unauthenticated, "UNAUTHENTICATED"},
            };

            Assert.Equal(tc.SpanStatus, d[span.Status.CanonicalCode]);
            if (tc.SpanStatusHasDescription.HasValue)
                Assert.Equal(tc.SpanStatusHasDescription.Value, !string.IsNullOrEmpty(span.Status.Description));

            var normalizedAttributes = span.Attributes.ToDictionary(
                x => x.Key,
                x => x.Value.ToString());
            tc.SpanAttributes = tc.SpanAttributes.ToDictionary(
                x => x.Key,
                x =>
                {
                    if (x.Key == "http.flavor" && x.Value == "2.0")
                        return "1.1";
                    return HttpTestData.NormalizeValues(x.Value, host, port);
                });

            Assert.Equal(tc.SpanAttributes, normalizedAttributes);
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
