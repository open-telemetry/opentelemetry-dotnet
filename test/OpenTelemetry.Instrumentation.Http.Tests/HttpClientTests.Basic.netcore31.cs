// <copyright file="HttpClientTests.Basic.netcore31.cs" company="OpenTelemetry Authors">
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
#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Context;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Instrumentation.Http.Tests
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
        public void AddHttpClientInstrumentation_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddHttpClientInstrumentation());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HttpClientInstrumentationInjectsHeadersAsync(bool shouldEnrich)
        {
            var processor = new Mock<BaseProcessor<Activity>>();
            processor.Setup(x => x.OnStart(It.IsAny<Activity>())).Callback<Activity>(c => c.SetTag("enriched", "no"));
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

            // var isInjectedHeaderValueGetterThrows = false;
            // mockTextFormat
            //     .Setup(x => x.IsInjected(It.IsAny<HttpRequestMessage>(), It.IsAny<Func<HttpRequestMessage, string, IEnumerable<string>>>()))
            //     .Callback<HttpRequestMessage, Func<HttpRequestMessage, string, IEnumerable<string>>>(
            //         (carrier, getter) =>
            //         {
            //             try
            //             {
            //                 // traceparent doesn't exist
            //                 getter(carrier, "traceparent");
            //             }
            //             catch
            //             {
            //                 isInjectedHeaderValueGetterThrows = true;
            //             }
            //         });

            using (Sdk.CreateTracerProviderBuilder()
                        .AddHttpClientInstrumentation(o =>
                        {
                            if (shouldEnrich)
                            {
                                o.Enrich = ActivityEnrichmentSetTag;
                            }
                        })
                        .AddProcessor(processor.Object)
                        .Build())
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.
            var activity = (Activity)processor.Invocations[2].Arguments[0];

            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

            Assert.True(request.Headers.TryGetValues("traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("tracestate", out var tracestates));
            Assert.Single(traceparents);
            Assert.Single(tracestates);

            Assert.Equal($"00-{activity.Context.TraceId}-{activity.Context.SpanId}-01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());

            Assert.NotEmpty(activity.Tags.Where(tag => tag.Key == "enriched"));
            Assert.Equal(shouldEnrich ? "yes" : "no", activity.Tags.Where(tag => tag.Key == "enriched").FirstOrDefault().Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HttpClientInstrumentationInjectsHeadersAsync_CustomFormat(bool shouldEnrich)
        {
            var propagator = new Mock<TextMapPropagator>();
            propagator.Setup(m => m.Inject<HttpRequestMessage>(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, message, action) =>
                {
                    action(message, "custom_traceparent", $"00/{context.ActivityContext.TraceId}/{context.ActivityContext.SpanId}/01");
                    action(message, "custom_tracestate", Activity.Current.TraceStateString);
                });

            var processor = new Mock<BaseProcessor<Activity>>();

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

            Sdk.SetDefaultTextMapPropagator(propagator.Object);

            using (Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation((opt) =>
                   {
                       if (shouldEnrich)
                       {
                           opt.Enrich = ActivityEnrichment;
                       }
                   })
                   .AddProcessor(processor.Object)
                   .Build())
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.
            var activity = (Activity)processor.Invocations[2].Arguments[0];

            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

            Assert.True(request.Headers.TryGetValues("custom_traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("custom_tracestate", out var tracestates));
            Assert.Single(traceparents);
            Assert.Single(tracestates);

            Assert.Equal($"00/{activity.Context.TraceId}/{activity.Context.SpanId}/01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                new BaggagePropagator(),
            }));
        }

        [Fact]
        public async Task HttpClientInstrumentationRespectsSuppress()
        {
            try
            {
                var propagator = new Mock<TextMapPropagator>();
                propagator.Setup(m => m.Inject<HttpRequestMessage>(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                    .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, message, action) =>
                    {
                        action(message, "custom_traceparent", $"00/{context.ActivityContext.TraceId}/{context.ActivityContext.SpanId}/01");
                        action(message, "custom_tracestate", Activity.Current.TraceStateString);
                    });

                var processor = new Mock<BaseProcessor<Activity>>();

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

                Sdk.SetDefaultTextMapPropagator(propagator.Object);

                using (Sdk.CreateTracerProviderBuilder()
                       .AddHttpClientInstrumentation()
                       .AddProcessor(processor.Object)
                       .Build())
                {
                    using var c = new HttpClient();
                    using (SuppressInstrumentationScope.Begin())
                    {
                        await c.SendAsync(request);
                    }
                }

                // If suppressed, activity is not emitted and
                // propagation is also not performed.
                Assert.Equal(3, processor.Invocations.Count); // SetParentProvider/OnShutdown/Dispose called.
                Assert.False(request.Headers.Contains("custom_traceparent"));
                Assert.False(request.Headers.Contains("custom_tracestate"));
            }
            finally
            {
                Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
                {
                    new TraceContextPropagator(),
                    new BaggagePropagator(),
                }));
            }
        }

        [Fact]
        public async Task HttpClientInstrumentation_AddViaFactory_HttpInstrumentation_CollectsSpans()
        {
            var processor = new Mock<BaseProcessor<Activity>>();

            using (Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Single(processor.Invocations.Where(i => i.Method.Name == "OnStart"));
            Assert.Single(processor.Invocations.Where(i => i.Method.Name == "OnEnd"));
            Assert.IsType<Activity>(processor.Invocations[1].Arguments[0]);
        }

        [Fact]
        public async Task HttpClientInstrumentationBacksOffIfAlreadyInstrumented()
        {
            // TODO: Investigate why this feature is required.
            var processor = new Mock<BaseProcessor<Activity>>();

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");

            using (Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation()
                   .AddProcessor(processor.Object)
                   .Build())
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(4, processor.Invocations.Count); // SetParentProvider/OnShutdown/Dispose/OnStart called.
        }

        [Fact]
        public async void RequestNotCollectedWhenInstrumentationFilterApplied()
        {
            var processor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                               .AddHttpClientInstrumentation(
                        (opt) => opt.Filter = (req) => !req.RequestUri.OriginalString.Contains(this.url))
                               .AddProcessor(processor.Object)
                               .Build())
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url);
            }

            Assert.Equal(4, processor.Invocations.Count); // SetParentProvider/OnShutdown/Dispose/OnStart called.
        }

        [Fact]
        public async void RequestNotCollectedWhenInstrumentationFilterThrowsException()
        {
            var processor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                               .AddHttpClientInstrumentation(
                        (opt) => opt.Filter = (req) => throw new Exception("From InstrumentationFilter"))
                               .AddProcessor(processor.Object)
                               .Build())
            {
                using var c = new HttpClient();
                using (var inMemoryEventListener = new InMemoryEventListener(HttpInstrumentationEventSource.Log))
                {
                    await c.GetAsync(this.url);
                    Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 4));
                }
            }

            Assert.Equal(4, processor.Invocations.Count); // SetParentProvider/OnShutdown/Dispose/OnStart called.
        }

        [Fact]
        public async Task HttpClientInstrumentationCorrelationAndBaggage()
        {
            var activityProcessor = new Mock<BaseProcessor<Activity>>();

            using var parent = new Activity("w3c activity");
            parent.SetIdFormat(ActivityIdFormat.W3C);
            parent.AddBaggage("k1", "v1");
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;
            parent.Start();

            Baggage.SetBaggage("k2", "v2");

            using (Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation(options => options.Enrich = ActivityEnrichment)
                .AddProcessor(activityProcessor.Object)
                .Build())
            {
                using var c = new HttpClient();
                using var r = await c.GetAsync("https://opentelemetry.io/").ConfigureAwait(false);
            }

            Assert.Equal(5, activityProcessor.Invocations.Count);
        }

        [Fact]
        public async Task HttpClientInstrumentationContextPropagation()
        {
            var processor = new Mock<BaseProcessor<Activity>>();
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
            Baggage.SetBaggage("b1", "v1");
            using (Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddProcessor(processor.Object)
                .Build())
            {
                using var c = new HttpClient();
                await c.SendAsync(request);
            }

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.
            var activity = (Activity)processor.Invocations[1].Arguments[0];

            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

            Assert.True(request.Headers.TryGetValues("traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("tracestate", out var tracestates));
            Assert.True(request.Headers.TryGetValues("baggage", out var baggages));
            Assert.Single(traceparents);
            Assert.Single(tracestates);
            Assert.Single(baggages);

            Assert.Equal($"00-{activity.Context.TraceId}-{activity.Context.SpanId}-01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());
            Assert.Equal("b1=v1", baggages.Single());
        }

        public void Dispose()
        {
            this.serverLifeTime?.Dispose();
            Activity.Current = null;
        }

        private static void ActivityEnrichmentSetTag(Activity activity, string method, object obj)
        {
            ActivityEnrichment(activity, method, obj);
            activity.SetTag("enriched", "yes");
        }

        private static void ActivityEnrichment(Activity activity, string method, object obj)
        {
            switch (method)
            {
                case "OnStartActivity":
                    Assert.True(obj is HttpRequestMessage);
                    break;

                case "OnStopActivity":
                    Assert.True(obj is HttpResponseMessage);
                    break;

                case "OnException":
                    Assert.True(obj is Exception);
                    break;

                default:
                    break;
            }
        }
    }
}
#endif
