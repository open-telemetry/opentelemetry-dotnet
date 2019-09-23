// <copyright file="BasicTests.cs" company="OpenTelemetry Authors">
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

using System.Linq;

namespace OpenTelemetry.Collector.Dependencies.Tests
{
    using Moq;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;
    using System;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Xunit;

    public partial class HttpClientTests : IDisposable
    {
        private readonly IDisposable serverLifeTime;
        private readonly string url;
        public HttpClientTests()
        {
            this.serverLifeTime = TestServer.RunServer(
                (ctx) =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.Close();
                },
                out var host,
                out var port);

            this.url = $"http://{host}:{port}/";
        }

        [Fact]
        public async Task HttpDependenciesCollectorInjectsHeadersAsync()
        {
            var spanProcessor = new Mock<SpanProcessor>(new NoopSpanExporter());
            var tracer = new Tracer(spanProcessor.Object, TraceConfig.Default, null, null);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = new HttpMethod("GET"),
            };

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";

            using (new DependenciesCollector(new DependenciesCollectorOptions(), tracer, Samplers.AlwaysSample))
            using (var c = new HttpClient())
            {
                await c.SendAsync(request);
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = ((Span)spanProcessor.Invocations[1].Arguments[0]);

            Assert.Equal(parent.TraceId, span.Context.TraceId);
            Assert.Equal(parent.SpanId, span.ParentSpanId);
            Assert.NotEqual(parent.SpanId, span.Context.SpanId);
            Assert.NotEqual(default, span.Context.SpanId);

            Assert.True(request.Headers.TryGetValues("traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("tracestate", out var tracestates));
            Assert.Single(traceparents);
            Assert.Single(tracestates);

            Assert.Equal($"00-{span.Context.TraceId}-{span.Context.SpanId}-01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());
        }

        [Fact]
        public async Task HttpDependenciesCollectorBacksOffIfAlreadyInstrumented()
        {
            var spanProcessor = new Mock<SpanProcessor>(new NoopSpanExporter());
            var tracer = new Tracer(spanProcessor.Object, TraceConfig.Default, null, null);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = new HttpMethod("GET"),
            };

            request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");

            using (new DependenciesCollector(new DependenciesCollectorOptions(), tracer, Samplers.AlwaysSample))
            using (var c = new HttpClient())
            {
                await c.SendAsync(request);
            }

            Assert.Equal(0, spanProcessor.Invocations.Count); 
        }

        public void Dispose()
        {
            serverLifeTime?.Dispose();
            Activity.Current = null;
        }
    }
}
