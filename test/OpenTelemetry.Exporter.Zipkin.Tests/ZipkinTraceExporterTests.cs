// <copyright file="ZipkinTraceExporterTests.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal.Test;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Exporter.Zipkin.Tests.Implementation;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests
{
    public class ZipkinTraceExporterTests : IDisposable
    {
        private static readonly ConcurrentDictionary<Guid, string> Responses = new ConcurrentDictionary<Guid, string>();

        private readonly IDisposable testServer;
        private readonly string testServerHost;
        private readonly int testServerPort;

        public ZipkinTraceExporterTests()
        {
            this.testServer = TestHttpServer.RunServer(
                ctx => ProcessServerRequest(ctx),
                out this.testServerHost,
                out this.testServerPort);

            static void ProcessServerRequest(HttpListenerContext context)
            {
                context.Response.StatusCode = 200;

                using StreamReader readStream = new StreamReader(context.Request.InputStream);

                string requestContent = readStream.ReadToEnd();

                Responses.TryAdd(
                    Guid.Parse(context.Request.QueryString["requestId"]),
                    requestContent);

                context.Response.OutputStream.Close();
            }
        }

        public void Dispose()
        {
            this.testServer.Dispose();
        }

        [Fact]
        public async Task ZipkinExporterIntegrationTest()
        {
            var spans = new List<SpanData> { ZipkinTraceExporterRemoteEndpointTests.CreateTestSpan() };

            Guid requestId = Guid.NewGuid();

            ZipkinTraceExporter exporter = new ZipkinTraceExporter(
                new ZipkinTraceExporterOptions
                {
                    Endpoint = new Uri($"http://{this.testServerHost}:{this.testServerPort}/api/v2/spans?requestId={requestId}"),
                });

            await exporter.ExportAsync(spans, CancellationToken.None).ConfigureAwait(false);

            await exporter.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);

            var span = spans[0];
            var context = span.Context;

            var timestamp = ZipkinConversionExtensions.ToEpochMicroseconds(span.StartTimestamp);

            StringBuilder ipInformation = new StringBuilder();
            if (!string.IsNullOrEmpty(exporter.LocalEndpoint.Ipv4))
                ipInformation.Append($@",""ipv4"":""{exporter.LocalEndpoint.Ipv4}""");
            if (!string.IsNullOrEmpty(exporter.LocalEndpoint.Ipv6))
                ipInformation.Append($@",""ipv6"":""{exporter.LocalEndpoint.Ipv6}""");

            Assert.Equal(
                $@"[{{""traceId"":""e8ea7e9ac72de94e91fabc613f9686b2"",""name"":""Name"",""parentId"":""{ZipkinConversionExtensions.EncodeSpanId(span.ParentSpanId)}"",""id"":""{ZipkinConversionExtensions.EncodeSpanId(context.SpanId)}"",""kind"":""CLIENT"",""timestamp"":{timestamp},""duration"":60000000,""localEndpoint"":{{""serviceName"":""Open Telemetry Exporter""{ipInformation}}},""annotations"":[{{""timestamp"":{timestamp},""value"":""Event1""}},{{""timestamp"":{timestamp},""value"":""Event2""}}],""tags"":{{""stringKey"":""value"",""longKey"":""1"",""longKey2"":""1"",""doubleKey"":""1"",""doubleKey2"":""1"",""boolKey"":""True"",""ot.status_code"":""Ok""}}}}]",
                Responses[requestId]);
        }
    }
}
