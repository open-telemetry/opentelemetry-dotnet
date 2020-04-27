// <copyright file="HttpClientTests.Basic.netcore31.cs" company="OpenTelemetry Authors">
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
#if NETCOREAPP3_1
using OpenTelemetry.Trace.Configuration;
using Moq;
using OpenTelemetry.Internal.Test;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Context.Propagation;
using Xunit;

namespace OpenTelemetry.Adapter.Dependencies.Tests
{
    public partial class HttpClientTests : IDisposable
    {
        private readonly IDisposable serverLifeTime;
        private readonly string url;

        public HttpClientTests()
        {
            this.serverLifeTime = TestHttpServer.RunServer(
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
        public void AddDependencyAdapter_BadArgs()
        {
            TracerBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddDependencyAdapter());
            Assert.Throws<ArgumentNullException>(() => builder.AddDependencyAdapter(null, null));
        }

        [Fact]
        public async Task HttpDependenciesAdapterInjectsHeadersAsync()
        {
            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b.AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using (new HttpClientAdapter(tracer, new HttpClientAdapterOptions()))
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

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
        public async Task HttpDependenciesAdapterInjectsHeadersAsync_CustomFormat()
        {
            var textFormat = new Mock<ITextFormat>();
            textFormat.Setup(m => m.Inject<HttpRequestMessage>(It.IsAny<SpanContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                .Callback<SpanContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, message, action) =>
                {
                    action(message, "custom_traceparent", $"00/{context.TraceId}/{context.SpanId}/01");
                    action(message, "custom_tracestate", Activity.Current.TraceStateString);
                });

            var spanProcessor = new Mock<SpanProcessor>();
            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using (new HttpClientAdapter(tracer, new HttpClientAdapterOptions { TextFormat = textFormat.Object }))
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(2, spanProcessor.Invocations.Count); // begin and end was called
            var span = (SpanData)spanProcessor.Invocations[1].Arguments[0];

            Assert.Equal(parent.TraceId, span.Context.TraceId);
            Assert.Equal(parent.SpanId, span.ParentSpanId);
            Assert.NotEqual(parent.SpanId, span.Context.SpanId);
            Assert.NotEqual(default, span.Context.SpanId);

            Assert.True(request.Headers.TryGetValues("custom_traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("custom_tracestate", out var tracestates));
            Assert.Single(traceparents);
            Assert.Single(tracestates);

            Assert.Equal($"00/{span.Context.TraceId}/{span.Context.SpanId}/01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());
        }

        [Fact]
        public async Task HttpDependenciesAdapter_AddViaFactory_HttpAdapter_CollectsSpans()
        {
            var spanProcessor = new Mock<SpanProcessor>();

            using (TracerFactory.Create(b => b
                .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object))
                .AddAdapter(t => new HttpClientAdapter(t))))
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

            using (new HttpClientAdapter(tracer, new HttpClientAdapterOptions()))
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

            var options = new HttpClientAdapterOptions((activityName, arg1, _) => !(activityName == "System.Net.Http.HttpRequestOut" &&
                                                                                        arg1 is HttpRequestMessage request &&
                                                                                        request.RequestUri.OriginalString.Contains(this.url)));

            using (new HttpClientAdapter(tracer, options))
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Equal(0, spanProcessor.Invocations.Count);
        }

        [Fact]
        public async Task HttpDependenciesAdapterFiltersOutRequestsToExporterEndpoints()
        {
            var spanProcessor = new Mock<SpanProcessor>();

            var tracer = TracerFactory.Create(b => b
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(null);

            var options = new HttpClientAdapterOptions();

            using (new HttpClientAdapter(tracer, options))
            {
                using var c = new HttpClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                try
                {
                    await c.PostAsync("https://dc.services.visualstudio.com/", new StringContent(""), cts.Token);
                    await c.PostAsync("https://localhost:9411/api/v2/spans", new StringContent(""), cts.Token);
                }
                catch
                {
                    // ignore all, whatever response is, we don't want anything tracked
                }
            }

            Assert.Equal(0, spanProcessor.Invocations.Count);
        }

        public void Dispose()
        {
            this.serverLifeTime?.Dispose();
            Activity.Current = null;
        }
    }
}
#endif
