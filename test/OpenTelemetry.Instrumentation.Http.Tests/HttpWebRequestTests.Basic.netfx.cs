﻿// <copyright file="HttpWebRequestTests.Basic.netfx.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests
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

        public void Dispose()
        {
            this.serverLifeTime?.Dispose();
        }

        [Fact]
        public async Task HttpWebRequestInstrumentationInjectsHeadersAsync()
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddHttpWebRequestInstrumentation()
                .Build();

            var request = (HttpWebRequest)WebRequest.Create(this.url);

            request.Method = "GET";

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using var response = await request.GetResponseAsync();

            Assert.Equal(2, activityProcessor.Invocations.Count); // begin and end was called
            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

            string traceparent = request.Headers.Get("traceparent");
            string tracestate = request.Headers.Get("tracestate");

            Assert.Equal($"00-{activity.Context.TraceId}-{activity.Context.SpanId}-01", traceparent);
            Assert.Equal("k1=v1,k2=v2", tracestate);

            parent.Stop();
        }

        [Fact]
        public async Task HttpWebRequestInstrumentationInjectsHeadersAsync_CustomFormat()
        {
            var propagator = new Mock<IPropagator>();
            propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpWebRequest>(), It.IsAny<Action<HttpWebRequest, string, string>>()))
                .Callback<PropagationContext, HttpWebRequest, Action<HttpWebRequest, string, string>>((context, message, action) =>
                {
                    action(message, "custom_traceparent", $"00/{context.ActivityContext.TraceId}/{context.ActivityContext.SpanId}/01");
                    action(message, "custom_tracestate", Activity.Current.TraceStateString);
                });

            var activityProcessor = new Mock<ActivityProcessor>();
            using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddHttpWebRequestInstrumentation(options => options.Propagator = propagator.Object)
                .Build();

            var request = (HttpWebRequest)WebRequest.Create(this.url);

            request.Method = "GET";

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using var response = await request.GetResponseAsync();

            Assert.Equal(2, activityProcessor.Invocations.Count); // begin and end was called

            var activity = (Activity)activityProcessor.Invocations[1].Arguments[0];

            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

            string traceparent = request.Headers.Get("custom_traceparent");
            string tracestate = request.Headers.Get("custom_tracestate");

            Assert.Equal($"00/{activity.Context.TraceId}/{activity.Context.SpanId}/01", traceparent);
            Assert.Equal("k1=v1,k2=v2", tracestate);

            parent.Stop();
        }

        [Fact]
        public async Task HttpWebRequestInstrumentationBacksOffIfAlreadyInstrumented()
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddHttpWebRequestInstrumentation()
                .Build();

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");

            using var c = new HttpClient();
            await c.SendAsync(request);

            Assert.Equal(0, activityProcessor.Invocations.Count);
        }

        [Fact]
        public async Task RequestNotCollectedWhenInstrumentationFilterApplied()
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddHttpWebRequestInstrumentation(
                    c => c.Filter = (req) => !req.RequestUri.OriginalString.Contains(this.url))
                .Build();

            using var c = new HttpClient();
            await c.GetAsync(this.url);

            Assert.Equal(0, activityProcessor.Invocations.Count);
        }

        [Fact]
        public async Task RequestNotCollectedWhenInstrumentationFilterThrowsException()
        {
            var activityProcessor = new Mock<ActivityProcessor>();
            using var shutdownSignal = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddHttpWebRequestInstrumentation(
                    c => c.Filter = (req) => throw new Exception("From Instrumentation filter"))
                .Build();

            using var c = new HttpClient();
            using (var inMemoryEventListener = new InMemoryEventListener(HttpInstrumentationEventSource.Log))
            {
                await c.GetAsync(this.url);
                Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 4));
            }
        }
    }
}
#endif
