// <copyright file="HttpClientTests.Basic.cs" company="OpenTelemetry Authors">
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
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
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
                    if (ctx.Request.Url.PathAndQuery.Contains("500"))
                    {
                        ctx.Response.StatusCode = 500;
                    }
                    else if (ctx.Request.Url.PathAndQuery.Contains("redirect"))
                    {
                        ctx.Response.RedirectLocation = "/";
                        ctx.Response.StatusCode = 302;
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

        [Fact]
        public void AddHttpClientInstrumentation_NamedOptions()
        {
            int defaultExporterOptionsConfigureOptionsInvocations = 0;
            int namedExporterOptionsConfigureOptionsInvocations = 0;

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .ConfigureServices(services =>
                {
                    services.Configure<HttpClientInstrumentationOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                    services.Configure<HttpClientInstrumentationOptions>("Instrumentation2", o => namedExporterOptionsConfigureOptionsInvocations++);
                })
                .AddHttpClientInstrumentation()
                .AddHttpClientInstrumentation("Instrumentation2", configureHttpClientInstrumentationOptions: null)
                .Build();

            Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
            Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
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
            propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
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
                propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
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
        public async Task HttpClientInstrumentationExportsSpansCreatedForRetries()
        {
            var exportedItems = new List<Activity>();
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation()
                   .AddInMemoryExporter(exportedItems)
                   .Build();

            int maxRetries = 3;
            using var c = new HttpClient(new RetryHandler(new HttpClientHandler(), maxRetries));
            await c.SendAsync(request);

            // number of exported spans should be 3(maxRetries)
            Assert.Equal(maxRetries, exportedItems.Count());

            var spanid1 = exportedItems[0].SpanId;
            var spanid2 = exportedItems[1].SpanId;
            var spanid3 = exportedItems[2].SpanId;

            // Validate span ids are different
            Assert.NotEqual(spanid1, spanid2);
            Assert.NotEqual(spanid3, spanid1);
            Assert.NotEqual(spanid2, spanid3);
        }

        [Fact]
        public async Task HttpClientRedirectTest()
        {
            var processor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                       .AddHttpClientInstrumentation()
                       .AddProcessor(processor.Object)
                       .Build())
            {
                using var c = new HttpClient();
                await c.GetAsync($"{this.url}redirect");
            }

            Assert.Equal(7, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnStart/OnEnd/OnShutdown/Dispose called.

            var firstActivity = (Activity)processor.Invocations[2].Arguments[0]; // First OnEnd
            Assert.Contains(firstActivity.TagObjects, t => t.Key == "http.status_code" && (int)t.Value == 302);

            var secondActivity = (Activity)processor.Invocations[4].Arguments[0]; // Second OnEnd
            Assert.Contains(secondActivity.TagObjects, t => t.Key == "http.status_code" && (int)t.Value == 200);
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
                using var inMemoryEventListener = new InMemoryEventListener(HttpInstrumentationEventSource.Log);
                await c.GetAsync(this.url);
                Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 4));
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
                using var r = await c.GetAsync(this.url).ConfigureAwait(false);
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

        [Fact]
        public async Task HttpClientInstrumentationReportsExceptionEventForNetworkFailuresWithGetAsync()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation(o => o.RecordException = true)
                   .AddInMemoryExporter(exportedItems)
                   .Build();

            using var c = new HttpClient();
            try
            {
                await c.GetAsync("https://sdlfaldfjalkdfjlkajdflkajlsdjf.sdlkjafsdjfalfadslkf.com/");
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
        public async Task HttpClientInstrumentationDoesNotReportExceptionEventOnErrorResponseWithGetAsync()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation(o => o.RecordException = true)
                   .AddInMemoryExporter(exportedItems)
                   .Build();

            using var c = new HttpClient();
            try
            {
                await c.GetAsync($"{this.url}500");
            }
            catch
            {
                exceptionThrown = true;
            }

            // Exception is not thrown and not collected as event
            Assert.False(exceptionThrown);
            Assert.Empty(exportedItems[0].Events);
        }

        [Fact]
        public async Task HttpClientInstrumentationDoesNotReportExceptionEventOnErrorResponseWithGetStringAsync()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"{this.url}500"),
                Method = new HttpMethod("GET"),
            };

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                   .AddHttpClientInstrumentation(o => o.RecordException = true)
                   .AddInMemoryExporter(exportedItems)
                   .Build();

            using var c = new HttpClient();
            try
            {
                await c.GetStringAsync($"{this.url}500");
            }
            catch
            {
                exceptionThrown = true;
            }

            // Exception is thrown and not collected as event
            Assert.True(exceptionThrown);
            Assert.Empty(exportedItems[0].Events);
        }

        public void Dispose()
        {
            this.serverLifeTime?.Dispose();
            Activity.Current = null;
            GC.SuppressFinalize(this);
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
