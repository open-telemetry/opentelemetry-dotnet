// <copyright file="HttpWebRequestTests.Basic.net461.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Adapter.Dependencies.Implementation;
using Xunit;

namespace OpenTelemetry.Adapter.Dependencies.Tests
{
    public partial class HttpWebRequestTests : IDisposable
    {
        private readonly IDisposable serverLifeTime;
        private readonly string url;

        public HttpWebRequestTests()
        {
            Assert.Null(Activity.Current);
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = false;

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

        public void Dispose()
        {
            this.serverLifeTime?.Dispose();
        }

        [Fact]
        public async Task HttpDependenciesAdapterInjectsHeadersAsync()
        {
            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b.AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            var request = (HttpWebRequest)WebRequest.Create(this.url);

            request.Method = "GET";

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using (new HttpWebRequestAdapter(tracer, new HttpClientAdapterOptions()))
            {
                using var response = await request.GetResponseAsync();
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(parent.TraceId, span.Context.TraceId);
            Assert.Equal(parent.SpanId, span.ParentSpanId);
            Assert.NotEqual(parent.SpanId, span.Context.SpanId);
            Assert.NotEqual(default, span.Context.SpanId);

            string traceparent = request.Headers.Get("traceparent");
            string tracestate = request.Headers.Get("tracestate");

            Assert.Equal($"00-{span.Context.TraceId}-{span.Context.SpanId}-01", traceparent);
            Assert.Equal("k1=v1,k2=v2", tracestate);

            parent.Stop();
        }

        [Fact]
        public async Task HttpDependenciesAdapterInjectsHeadersAsync_CustomFormat()
        {
            var textFormat = new Mock<ITextFormat>();
            textFormat.Setup(m => m.Inject<HttpWebRequest>(It.IsAny<SpanContext>(), It.IsAny<HttpWebRequest>(), It.IsAny<Action<HttpWebRequest, string, string>>()))
                .Callback<SpanContext, HttpWebRequest, Action<HttpWebRequest, string, string>>((context, message, action) =>
                {
                    action(message, "custom_traceparent", $"00/{context.TraceId}/{context.SpanId}/01");
                    action(message, "custom_tracestate", Activity.Current.TraceStateString);
                });

            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            var request = (HttpWebRequest)WebRequest.Create(this.url);

            request.Method = "GET";

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using (new HttpWebRequestAdapter(tracer, new HttpClientAdapterOptions { TextFormat = textFormat.Object }))
            {
                using var response = await request.GetResponseAsync();
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called

            var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(parent.TraceId, span.Context.TraceId);
            Assert.Equal(parent.SpanId, span.ParentSpanId);
            Assert.NotEqual(parent.SpanId, span.Context.SpanId);
            Assert.NotEqual(default, span.Context.SpanId);

            string traceparent = request.Headers.Get("custom_traceparent");
            string tracestate = request.Headers.Get("custom_tracestate");

            Assert.Equal($"00/{span.Context.TraceId}/{span.Context.SpanId}/01", traceparent);
            Assert.Equal("k1=v1,k2=v2", tracestate);

            parent.Stop();
        }

        [Fact]
        public async Task HttpDependenciesAdapter_AddViaFactory_HttpAdapter_CollectsSpans()
        {
            var spanProcessor = new Mock<SpanProcessor>();

            using (TracerFactory.Create(b => b
                .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object))
                .AddAdapter(t => new HttpWebRequestAdapter(t))))
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Single(spanProcessor.Invocations.Where(i => i.Method.Name == "OnStart"));
            Assert.Single(spanProcessor.Invocations.Where(i => i.Method.Name == "OnEnd"));
            Assert.IsType<SpanData>(spanProcessor.Invocations[1].Arguments[0]);
        }

        [Fact]
        public async Task HttpDependenciesAdapter_AddViaFactory_DependencyAdapter_CollectsSpans()
        {
            var spanProcessor = new Mock<SpanProcessor>();

            using (TracerFactory.Create(b => b
                .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object))
                .AddDependencyAdapter()))
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Single(spanProcessor.Invocations.Where(i => i.Method.Name == "OnStart"));
            Assert.Single(spanProcessor.Invocations.Where(i => i.Method.Name == "OnEnd"));
            Assert.IsType<SpanData>(spanProcessor.Invocations[1].Arguments[0]);
        }

        [Fact]
        public async Task HttpDependenciesAdapterBacksOffIfAlreadyInstrumented()
        {
            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");

            using (new HttpWebRequestAdapter(tracer, new HttpClientAdapterOptions()))
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(0, spanProcessor.Invocations.Count);
        }

        [Fact]
        public async Task HttpDependenciesAdapterFiltersOutRequests()
        {
            var spanProcessor = new Mock<SpanProcessor>();

            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            var options = new HttpClientAdapterOptions((activityName, arg1, _)
                => !(activityName == HttpWebRequestDiagnosticSource.ActivityName &&
                arg1 is HttpWebRequest request &&
                request.RequestUri.OriginalString.Contains(this.url)));

            using (new HttpWebRequestAdapter(tracer, options))
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Equal(0, spanProcessor.Invocations.Count);
        }
    }
}
#endif
