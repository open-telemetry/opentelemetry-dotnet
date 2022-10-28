// <copyright file="HttpWebRequestTests.Basic.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
#if !NETFRAMEWORK
using System.Net.Http;
#endif
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

#pragma warning disable SYSLIB0014 // Type or member is obsolete

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
                    if (string.IsNullOrWhiteSpace(ctx.Request.Headers["traceparent"])
                        && string.IsNullOrWhiteSpace(ctx.Request.Headers["custom_traceparent"])
                        && ctx.Request.QueryString["bypassHeaderCheck"] != "true")
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.StatusDescription = "Missing trace context";
                    }
                    else if (ctx.Request.Url.PathAndQuery.Contains("500"))
                    {
                        ctx.Response.StatusCode = 500;
                    }
                    else
                    {
                        ctx.Response.StatusCode = 200;
                    }

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
        public async Task BacksOffIfAlreadyInstrumented()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddHttpClientInstrumentation()
                .Build();

            var request = (HttpWebRequest)WebRequest.Create(this.url);

            request.Method = "GET";

            request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");

            using var response = await request.GetResponseAsync();

#if NETFRAMEWORK
            // Note: Back-off is part of the .NET Framework reflection only and
            // is needed to prevent issues when the same request is re-used for
            // things like redirects or SSL negotiation.
            Assert.Equal(1, activityProcessor.Invocations.Count); // SetParentProvider called
#else
            Assert.Equal(3, activityProcessor.Invocations.Count); // SetParentProvider/Begin/End called
#endif
        }

        [Fact]
        public async Task RequestNotCollectedWhenInstrumentationFilterApplied()
        {
            bool httpWebRequestFilterApplied = false;
            bool httpRequestMessageFilterApplied = false;

            List<Activity> exportedItems = new();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(exportedItems)
                .AddHttpClientInstrumentation(
                    options =>
                    {
                        options.FilterHttpWebRequest = (req) =>
                        {
                            httpWebRequestFilterApplied = true;
                            return !req.RequestUri.OriginalString.Contains(this.url);
                        };
                        options.FilterHttpRequestMessage = (req) =>
                        {
                            httpRequestMessageFilterApplied = true;
                            return !req.RequestUri.OriginalString.Contains(this.url);
                        };
                    })
                .Build();

            var request = (HttpWebRequest)WebRequest.Create($"{this.url}?bypassHeaderCheck=true");

            request.Method = "GET";

            using var response = await request.GetResponseAsync();

#if NETFRAMEWORK
            Assert.True(httpWebRequestFilterApplied);
            Assert.False(httpRequestMessageFilterApplied);
#else
            Assert.False(httpWebRequestFilterApplied);
            Assert.True(httpRequestMessageFilterApplied);
#endif

            Assert.Empty(exportedItems);
        }

        [Fact]
        public async Task RequestNotCollectedWhenInstrumentationFilterThrowsException()
        {
            List<Activity> exportedItems = new();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddInMemoryExporter(exportedItems)
                .AddHttpClientInstrumentation(
                    c =>
                    {
                        c.FilterHttpWebRequest = (req) => throw new Exception("From Instrumentation filter");
                        c.FilterHttpRequestMessage = (req) => throw new Exception("From Instrumentation filter");
                    })
                .Build();

            using (var inMemoryEventListener = new InMemoryEventListener(HttpInstrumentationEventSource.Log))
            {
                var request = (HttpWebRequest)WebRequest.Create($"{this.url}?bypassHeaderCheck=true");

                request.Method = "GET";

                using var response = await request.GetResponseAsync();

                Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 4));
            }

            Assert.Empty(exportedItems);
        }

        [Fact]
        public async Task InjectsHeadersAsync()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddProcessor(activityProcessor.Object)
                .AddHttpClientInstrumentation()
                .Build();

            var request = (HttpWebRequest)WebRequest.Create(this.url);

            request.Method = "GET";

            var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using var response = await request.GetResponseAsync();

            Assert.Equal(3, activityProcessor.Invocations.Count);  // SetParentProvider/Begin/End called
            var activity = (Activity)activityProcessor.Invocations[2].Arguments[0];

            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

#if NETFRAMEWORK
            string traceparent = request.Headers.Get("traceparent");
            string tracestate = request.Headers.Get("tracestate");

            Assert.Equal($"00-{activity.Context.TraceId}-{activity.Context.SpanId}-01", traceparent);
            Assert.Equal("k1=v1,k2=v2", tracestate);
#else
            // Note: On .NET HttpRequestMessage is created and enriched
            // not the HttpWebRequest that was executed.
            Assert.Empty(request.Headers);
#endif

            parent.Stop();
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task CustomPropagatorCalled(bool sample, bool createParentActivity)
        {
            ActivityContext parentContext = default;
            ActivityContext contextFromPropagator = default;

            var propagator = new Mock<TextMapPropagator>();
#if NETFRAMEWORK
            propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpWebRequest>(), It.IsAny<Action<HttpWebRequest, string, string>>()))
                .Callback<PropagationContext, HttpWebRequest, Action<HttpWebRequest, string, string>>((context, carrier, setter) =>
                {
                    contextFromPropagator = context.ActivityContext;

                    setter(carrier, "traceparent", $"00/{contextFromPropagator.TraceId}/{contextFromPropagator.SpanId}/01");
                    setter(carrier, "tracestate", contextFromPropagator.TraceState);
                });
#else
            propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, carrier, setter) =>
                {
                    contextFromPropagator = context.ActivityContext;

                    setter(carrier, "traceparent", $"00/{contextFromPropagator.TraceId}/{contextFromPropagator.SpanId}/01");
                    setter(carrier, "tracestate", contextFromPropagator.TraceState);
                });
#endif

            var exportedItems = new List<Activity>();

            using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .SetSampler(sample ? new ParentBasedSampler(new AlwaysOnSampler()) : new AlwaysOffSampler())
                .Build())
            {
                var previousDefaultTextMapPropagator = Propagators.DefaultTextMapPropagator;
                Sdk.SetDefaultTextMapPropagator(propagator.Object);

                Activity parent = null;
                if (createParentActivity)
                {
                    parent = new Activity("parent")
                        .SetIdFormat(ActivityIdFormat.W3C)
                        .Start();

                    parent.TraceStateString = "k1=v1,k2=v2";
                    parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

                    parentContext = parent.Context;
                }

                var request = (HttpWebRequest)WebRequest.Create(this.url);

                request.Method = "GET";

                using var response = await request.GetResponseAsync();

                parent?.Stop();

                Sdk.SetDefaultTextMapPropagator(previousDefaultTextMapPropagator);
            }

            if (!sample)
            {
                Assert.Empty(exportedItems);
            }
            else
            {
                Assert.Single(exportedItems);
            }

            Assert.True(contextFromPropagator != default);
            if (sample)
            {
                Assert.Equal(contextFromPropagator, exportedItems[0].Context);
            }

#if NETFRAMEWORK
            if (!sample && createParentActivity)
            {
                Assert.Equal(parentContext.TraceId, contextFromPropagator.TraceId);
                Assert.Equal(parentContext.SpanId, contextFromPropagator.SpanId);
            }
#endif
        }

        [Theory]
        [InlineData(null)]
        [InlineData("CustomName")]
        public void AddHttpClientInstrumentationUsesOptionsApi(string name)
        {
            name ??= Options.DefaultName;

            int configurationDelegateInvocations = 0;

            var activityProcessor = new Mock<BaseProcessor<Activity>>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<HttpClientInstrumentationOptions>(name, o => configurationDelegateInvocations++);
                })
                .AddProcessor(activityProcessor.Object)
                .AddHttpClientInstrumentation(name, options =>
                {
                    Assert.IsType<HttpClientInstrumentationOptions>(options);
                })
                .Build();

            Assert.Equal(1, configurationDelegateInvocations);
        }

        [Fact]
        public async Task ReportsExceptionEventForNetworkFailures()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation(o => o.RecordException = true)
                .AddInMemoryExporter(exportedItems)
                .Build();

            try
            {
                var request = (HttpWebRequest)WebRequest.Create("https://sdlfaldfjalkdfjlkajdflkajlsdjf.sdlkjafsdjfalfadslkf.com/");

                request.Method = "GET";

                using var response = await request.GetResponseAsync();
            }
            catch
            {
                exceptionThrown = true;
            }

            // Exception is thrown and collected as event
            Assert.True(exceptionThrown);
            Assert.Single(exportedItems[0].Events.Where(evt => evt.Name.Equals("exception")));
        }

        [Fact]
        public async Task ReportsExceptionEventOnErrorResponse()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation(o => o.RecordException = true)
                .AddInMemoryExporter(exportedItems)
                .Build();

            try
            {
                var request = (HttpWebRequest)WebRequest.Create($"{this.url}500");

                request.Method = "GET";

                using var response = await request.GetResponseAsync();
            }
            catch
            {
                exceptionThrown = true;
            }

#if NETFRAMEWORK
            // Exception is thrown and collected as event
            Assert.True(exceptionThrown);
            Assert.Single(exportedItems[0].Events.Where(evt => evt.Name.Equals("exception")));
#else
            // Note: On .NET Core exceptions through HttpWebRequest do not
            // trigger exception events they just throw:
            // https://github.com/dotnet/runtime/blob/cc5ba0994d6e8a6f5e4a63d1c921a68eda4350e8/src/libraries/System.Net.Requests/src/System/Net/HttpWebRequest.cs#L1371
            Assert.True(exceptionThrown);
            Assert.DoesNotContain(exportedItems[0].Events, evt => evt.Name.Equals("exception"));
#endif
        }
    }
}
