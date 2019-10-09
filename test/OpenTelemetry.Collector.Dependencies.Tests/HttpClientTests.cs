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

using OpenTelemetry.Resources;

namespace OpenTelemetry.Collector.Dependencies.Tests
{
    using Moq;
    using Newtonsoft.Json;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Configuration;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using Xunit;

    public partial class HttpClientTests
    {
        public class HttpOutTestCase
        {
            public string name { get; set; }

            public string method { get; set; }

            public string url { get; set; }

            public Dictionary<string, string> headers { get; set; }

            public int responseCode { get; set; }

            public string spanName { get; set; }

            public string spanKind { get; set; }

            public string spanStatus { get; set; }

            public Dictionary<string, string> spanAttributes { get; set; }
        }

        private static IEnumerable<object[]> readTestCases()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var serializer = new JsonSerializer();
            var input = serializer.Deserialize<HttpOutTestCase[]>(new JsonTextReader(new StreamReader(assembly.GetManifestResourceStream("OpenTelemetry.Collector.Dependencies.Tests.http-out-test-cases.json"))));

            return getArgumentsFromTestCaseObject(input);
        }

        private static IEnumerable<object[]> getArgumentsFromTestCaseObject(IEnumerable<HttpOutTestCase> input)
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

        public static IEnumerable<object[]> TestData => readTestCases();

        [Theory]
        [MemberData(nameof(TestData))]
        public async Task HttpOutCallsAreCollectedSuccessfullyAsync(HttpOutTestCase tc)
        {
            var serverLifeTime = TestServer.RunServer(
                (ctx) =>
                {
                    ctx.Response.StatusCode = tc.responseCode == 0 ? 200 : tc.responseCode;
                    ctx.Response.OutputStream.Close();
                },
                out var host, 
                out var port);

            var spanProcessor = new Mock<SpanProcessor>(new NoopSpanExporter());
            var tracerFactory = new TracerFactory(spanProcessor.Object);
            tc.url = NormalizeValues(tc.url, host, port);

            using (serverLifeTime)
            {
                using (var dc = new DependenciesCollector(new DependenciesCollectorOptions(), tracerFactory))
                {

                    try
                    {
                        using (var c = new HttpClient())
                        {
                            var request = new HttpRequestMessage
                            {
                                RequestUri = new Uri(tc.url),
                                Method = new HttpMethod(tc.method),
                            };

                            if (tc.headers != null)
                            {
                                foreach (var header in tc.headers)
                                {
                                    request.Headers.Add(header.Key, header.Value);
                                }
                            }

                            await c.SendAsync(request);
                        }
                    }
                    catch (Exception)
                    {
                        //test case can intentionally send request that will result in exception
                    }
                }
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = ((Span)spanProcessor.Invocations[1].Arguments[0]);

            Assert.Equal(tc.spanName, span.Name);
            Assert.Equal(tc.spanKind, span.Kind.ToString());

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

            Assert.Equal(tc.spanStatus, d[span.Status.CanonicalCode]);

            var normalizedAttributes = span.Attributes.ToDictionary(x => x.Key, x => x.Value.ToString());
            tc.spanAttributes = tc.spanAttributes.ToDictionary(x => x.Key, x => NormalizeValues(x.Value, host, port));

            Assert.Equal(tc.spanAttributes.ToHashSet(), normalizedAttributes.ToHashSet());
        }

        [Fact]
        public async Task DebugIndividualTestAsync()
        {
            var serializer = new JsonSerializer();
            var input = serializer.Deserialize<HttpOutTestCase[]>(new JsonTextReader(new StringReader(@"
[   {
    ""name"": ""Response code 404"",
    ""method"": ""GET"",
    ""url"": ""http://{host}:{port}/"",
    ""responseCode"": 404,
    ""spanName"": ""/"",
    ""spanStatus"": ""NOT_FOUND"",
    ""spanKind"": ""Client"",
    ""spanAttributes"": {
      ""http.path"": ""/"",
      ""http.method"": ""GET"",
      ""http.host"": ""{host}:{port}"",
      ""http.status_code"": ""404"",
      ""http.url"": ""http://{host}:{port}/""
}
        }
]
")));

            var t = (Task)this.GetType().InvokeMember(nameof(HttpOutCallsAreCollectedSuccessfullyAsync), BindingFlags.InvokeMethod, null, this, getArgumentsFromTestCaseObject(input).First());
            await t;
        }

        private string NormalizeValues(string value, string host, int port)
        {
            return value.Replace("{host}", host).Replace("{port}", port.ToString());
        }

    }
}
