// <copyright file="DurationTest.cs" company="OpenTelemetry Authors">
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
using Moq;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Collector.Dependencies.Tests
{
    public partial class HttpClientTests
    {
        public class HttpOutTestCase
        {
            public string Name { get; set; }

            public string Method { get; set; }

            public string Url { get; set; }

            public Dictionary<string, string> Headers { get; set; }

            public int ResponseCode { get; set; }

            public string SpanName { get; set; }

            public string SpanKind { get; set; }

            public string SpanStatus { get; set; }

            public bool? SpanStatusHasDescription { get; set; }

            public Dictionary<string, string> SpanAttributes { get; set; }

            public bool SetHttpFlavor { get; set; }
        }

        private static IEnumerable<object[]> ReadTestCases()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var serializer = new JsonSerializer();
            var input = serializer.Deserialize<HttpOutTestCase[]>(new JsonTextReader(new StreamReader(assembly.GetManifestResourceStream("OpenTelemetry.Collector.Dependencies.Tests.http-out-test-cases.json"))));

            return GetArgumentsFromTestCaseObject(input);
        }

        private static IEnumerable<object[]> GetArgumentsFromTestCaseObject(IEnumerable<HttpOutTestCase> input)
        {
            var result = new List<object[]>();

            foreach (var testCase in input)
            {
                result.Add(new object[] {
                    testCase,
                });
            }

            return result;
        }

        public static IEnumerable<object[]> TestData => ReadTestCases();

        [Theory]
        [MemberData(nameof(TestData))]
        public async Task HttpOutCallsAreCollectedSuccessfullyAsync(HttpOutTestCase tc)
        {
            var serverLifeTime = TestServer.RunServer(
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
            tc.Url = NormalizeValues(tc.Url, host, port);

            using (serverLifeTime)

            using (new HttpClientCollector(tracer, new HttpClientCollectorOptions() { SetHttpFlavor = tc.SetHttpFlavor }))
            {
                try
                {
                    using var c = new HttpClient();
                    var request = new HttpRequestMessage
                    {
                        RequestUri = new Uri(tc.Url),
                        Method = new HttpMethod(tc.Method),
                        Version = new Version(2, 0),
                    };

                    if (tc.Headers != null)
                    {
                        foreach (var header in tc.Headers)
                        {
                            request.Headers.Add(header.Key, header.Value);
                        }
                    }

                    await c.SendAsync(request);
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

            var d = new Dictionary<CanonicalCode, string>()
            {
                { CanonicalCode.Ok, "OK"},
                { CanonicalCode.Cancelled, "CANCELLED"},
                { CanonicalCode.Unknown, "UNKNOWN"},
                { CanonicalCode.InvalidArgument, "INVALID_ARGUMENT"},
                { CanonicalCode.DeadlineExceeded, "DEADLINE_EXCEEDED"},
                { CanonicalCode.NotFound, "NOT_FOUND"},
                { CanonicalCode.AlreadyExists, "ALREADY_EXISTS"},
                { CanonicalCode.PermissionDenied, "PERMISSION_DENIED"},
                { CanonicalCode.ResourceExhausted, "RESOURCE_EXHAUSTED"},
                { CanonicalCode.FailedPrecondition, "FAILED_PRECONDITION"},
                { CanonicalCode.Aborted, "ABORTED"},
                { CanonicalCode.OutOfRange, "OUT_OF_RANGE"},
                { CanonicalCode.Unimplemented, "UNIMPLEMENTED"},
                { CanonicalCode.Internal, "INTERNAL"},
                { CanonicalCode.Unavailable, "UNAVAILABLE"},
                { CanonicalCode.DataLoss, "DATA_LOSS"},
                { CanonicalCode.Unauthenticated, "UNAUTHENTICATED"},
            };

            Assert.Equal(tc.SpanStatus, d[span.Status.CanonicalCode]);
            if (tc.SpanStatusHasDescription.HasValue)
                Assert.Equal(tc.SpanStatusHasDescription.Value, !string.IsNullOrEmpty(span.Status.Description));

            var normalizedAttributes = span.Attributes.ToDictionary(x => x.Key, x => x.Value.ToString());
            tc.SpanAttributes = tc.SpanAttributes.ToDictionary(x => x.Key, x => NormalizeValues(x.Value, host, port));

            Assert.Equal(tc.SpanAttributes.ToHashSet(), normalizedAttributes.ToHashSet());
        }

        [Fact]
        public async Task DebugIndividualTestAsync()
        {
            var serializer = new JsonSerializer();
            var input = serializer.Deserialize<HttpOutTestCase[]>(new JsonTextReader(new StringReader(@"
[   {
    ""name"": ""Response code 404"",
    ""method"": ""GET"",
    ""url"": ""http://{host}:{port}/path/12314/?q=ddds#123"",
    ""responseCode"": 404,
    ""spanName"": ""/path/12314/"",
    ""spanStatus"": ""NOT_FOUND"",
    ""spanKind"": ""Client"",
    ""spanAttributes"": {
      ""component"": ""http"",
      ""http.method"": ""GET"",
      ""http.host"": ""{host}:{port}"",
      ""http.status_code"": ""404"",
      ""http.url"": ""http://{host}:{port}/path/12314/?q=ddds#123""
}
        }
]
")));

            var t = (Task)GetType().InvokeMember(nameof(HttpOutCallsAreCollectedSuccessfullyAsync), BindingFlags.InvokeMethod, null, this, GetArgumentsFromTestCaseObject(input).First());
            await t;
        }

        private string NormalizeValues(string value, string host, int port)
        {
            return value.Replace("{host}", host).Replace("{port}", port.ToString());
        }

    }
}
