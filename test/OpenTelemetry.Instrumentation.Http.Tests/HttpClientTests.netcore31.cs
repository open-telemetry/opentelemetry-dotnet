// <copyright file="HttpClientTests.netcore31.cs" company="OpenTelemetry Authors">
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
#if NETCOREAPP3_1
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using Newtonsoft.Json;
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
            var serverLifeTime = TestHttpServer.RunServer(
                (ctx) =>
                {
                    ctx.Response.StatusCode = tc.ResponseCode == 0 ? 200 : tc.ResponseCode;
                    ctx.Response.OutputStream.Close();
                },
                out var host,
                out var port);

            var expectedResource = Resources.Resources.CreateServiceResource("test-service");
            var spanProcessor = new Mock<ActivityProcessor>();
            tc.Url = HttpTestData.NormalizeValues(tc.Url, host, port);

            using (serverLifeTime)

            using (Sdk.CreateTracerProviderBuilder()
                               .AddHttpClientInstrumentation((opt) => opt.SetHttpFlavor = tc.SetHttpFlavor)
                               .AddProcessor(spanProcessor.Object)
                               .SetResource(expectedResource)
                               .Build())
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
                    // test case can intentionally send request that will result in exception
                }
            }

            Assert.Equal(3, spanProcessor.Invocations.Count); // start/end/dispose was called
            var span = (Activity)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(tc.SpanName, span.DisplayName);
            Assert.Equal(tc.SpanKind, span.Kind.ToString());

            var d = new Dictionary<string, string>()
            {
                { "Ok", "OK" },
                { "Cancelled", "CANCELLED" },
                { "Unknown", "UNKNOWN" },
                { "InvalidArgument", "INVALID_ARGUMENT" },
                { "DeadlineExceeded", "DEADLINE_EXCEEDED" },
                { "NotFound", "NOT_FOUND" },
                { "AlreadyExists", "ALREADY_EXISTS" },
                { "PermissionDenied", "PERMISSION_DENIED" },
                { "ResourceExhausted", "RESOURCE_EXHAUSTED" },
                { "FailedPrecondition", "FAILED_PRECONDITION" },
                { "Aborted", "ABORTED" },
                { "OutOfRange", "OUT_OF_RANGE" },
                { "Unimplemented", "UNIMPLEMENTED" },
                { "Internal", "INTERNAL" },
                { "Unavailable", "UNAVAILABLE" },
                { "DataLoss", "DATA_LOSS" },
                { "Unauthenticated", "UNAUTHENTICATED" },
            };

            // Assert.Equal(tc.SpanStatus, d[span.Status.CanonicalCode]);
            Assert.Equal(
                    tc.SpanStatus,
                    d[span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusCodeKey).Value]);

            if (tc.SpanStatusHasDescription.HasValue)
            {
                var desc = span.Tags.FirstOrDefault(i => i.Key == SpanAttributeConstants.StatusDescriptionKey).Value;
                Assert.Equal(tc.SpanStatusHasDescription.Value, !string.IsNullOrEmpty(desc));
            }

            var normalizedAttributes = span.TagObjects.Where(kv => !kv.Key.StartsWith("otel.")).ToImmutableSortedDictionary(x => x.Key, x => x.Value.ToString());
            var normalizedAttributesTestCase = tc.SpanAttributes.ToDictionary(x => x.Key, x => HttpTestData.NormalizeValues(x.Value, host, port));

            Assert.Equal(normalizedAttributesTestCase.Count, normalizedAttributes.Count);

            foreach (var kv in normalizedAttributesTestCase)
            {
                Assert.Contains(span.TagObjects, i => i.Key == kv.Key && i.Value.ToString().Equals(kv.Value, StringComparison.InvariantCultureIgnoreCase));
            }

            Assert.Equal(expectedResource, span.GetResource());
        }

        [Fact]
        public async Task DebugIndividualTestAsync()
        {
            var serializer = new JsonSerializer();
            var input = serializer.Deserialize<HttpTestData.HttpOutTestCase[]>(new JsonTextReader(new StringReader(@"
[
  {
    ""name"": ""Response code: 399"",
    ""method"": ""GET"",
    ""url"": ""http://{host}:{port}/"",
    ""responseCode"": 399,
    ""spanName"": ""HTTP GET"",
    ""spanStatus"": ""OK"",
    ""spanKind"": ""Client"",
    ""spanAttributes"": {
      ""http.method"": ""GET"",
      ""http.host"": ""{host}:{port}"",
      ""http.status_code"": ""399"",
      ""http.url"": ""http://{host}:{port}/""
    }
  }
]
")));

            var t = (Task)this.GetType().InvokeMember(nameof(this.HttpOutCallsAreCollectedSuccessfullyAsync), BindingFlags.InvokeMethod, null, this, HttpTestData.GetArgumentsFromTestCaseObject(input).First());
            await t;
        }
    }
}
#endif
