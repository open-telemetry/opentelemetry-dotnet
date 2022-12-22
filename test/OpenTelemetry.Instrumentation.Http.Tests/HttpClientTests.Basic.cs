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

using System.Diagnostics;
#if NETFRAMEWORK
using System.Net;
#endif
using System.Net.Http;
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
                    string traceparent = ctx.Request.Headers["traceparent"];
                    string custom_traceparent = ctx.Request.Headers["custom_traceparent"];
                    if (string.IsNullOrWhiteSpace(traceparent)
                        && string.IsNullOrWhiteSpace(custom_traceparent))
                    {
                        ctx.Response.StatusCode = 500;
                        ctx.Response.StatusDescription = "Missing trace context";
                    }
                    else if (ctx.Request.Url.PathAndQuery.Contains("500"))
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
        public async Task InjectsHeadersAsync(bool shouldEnrich)
        {
            var processor = new Mock<BaseProcessor<Activity>>();
            processor.Setup(x => x.OnStart(It.IsAny<Activity>())).Callback<Activity>(c =>
            {
                c.SetTag("enrichedWithHttpWebRequest", "no");
                c.SetTag("enrichedWithHttpWebResponse", "no");
                c.SetTag("enrichedWithHttpRequestMessage", "no");
                c.SetTag("enrichedWithHttpResponseMessage", "no");
            });

            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            using var parent = new Activity("parent")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parent.TraceStateString = "k1=v1,k2=v2";
            parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

            using (Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation(o =>
                {
                    if (shouldEnrich)
                    {
                        o.EnrichWithHttpWebRequest = (activity, httpWebRequest) =>
                        {
                            activity.SetTag("enrichedWithHttpWebRequest", "yes");
                        };

                        o.EnrichWithHttpWebResponse = (activity, httpWebResponse) =>
                        {
                            activity.SetTag("enrichedWithHttpWebResponse", "yes");
                        };

                        o.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("enrichedWithHttpRequestMessage", "yes");
                        };

                        o.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("enrichedWithHttpResponseMessage", "yes");
                        };
                    }
                })
                .AddProcessor(processor.Object)
                .Build())
            {
                using var c = new HttpClient();
                await c.SendAsync(request).ConfigureAwait(false);
            }

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.
            var activity = (Activity)processor.Invocations[2].Arguments[0];

            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

#if NETFRAMEWORK
            // Note: On .NET Framework a HttpWebRequest is created and enriched
            // not the HttpRequestMessage passed to HttpClient.
            Assert.Empty(request.Headers);
#else
            Assert.True(request.Headers.TryGetValues("traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("tracestate", out var tracestates));
            Assert.Single(traceparents);
            Assert.Single(tracestates);

            Assert.Equal($"00-{activity.Context.TraceId}-{activity.Context.SpanId}-01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());
#endif

#if NETFRAMEWORK
            Assert.Equal(shouldEnrich ? "yes" : "no", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpWebRequest").FirstOrDefault().Value);
            Assert.Equal(shouldEnrich ? "yes" : "no", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpWebResponse").FirstOrDefault().Value);

            Assert.Equal("no", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpRequestMessage").FirstOrDefault().Value);
            Assert.Equal("no", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpResponseMessage").FirstOrDefault().Value);
#else
            Assert.Equal("no", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpWebRequest").FirstOrDefault().Value);
            Assert.Equal("no", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpWebResponse").FirstOrDefault().Value);

            Assert.Equal(shouldEnrich ? "yes" : "no", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpRequestMessage").FirstOrDefault().Value);
            Assert.Equal(shouldEnrich ? "yes" : "no", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpResponseMessage").FirstOrDefault().Value);
#endif
        }

        [Fact]
        public async Task InjectsHeadersAsync_CustomFormat()
        {
            var propagator = new Mock<TextMapPropagator>();
            propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, message, action) =>
                {
                    action(message, "custom_traceparent", $"00/{context.ActivityContext.TraceId}/{context.ActivityContext.SpanId}/01");
                    action(message, "custom_tracestate", Activity.Current.TraceStateString);
                });

            var processor = new Mock<BaseProcessor<Activity>>();

            using var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            using var parent = new Activity("parent")
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
                await c.SendAsync(request).ConfigureAwait(false);
            }

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.
            var activity = (Activity)processor.Invocations[2].Arguments[0];

            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal(parent.TraceId, activity.Context.TraceId);
            Assert.Equal(parent.SpanId, activity.ParentSpanId);
            Assert.NotEqual(parent.SpanId, activity.Context.SpanId);
            Assert.NotEqual(default, activity.Context.SpanId);

#if NETFRAMEWORK
            // Note: On .NET Framework a HttpWebRequest is created and enriched
            // not the HttpRequestMessage passed to HttpClient.
            Assert.Empty(request.Headers);
#else
            Assert.True(request.Headers.TryGetValues("custom_traceparent", out var traceparents));
            Assert.True(request.Headers.TryGetValues("custom_tracestate", out var tracestates));
            Assert.Single(traceparents);
            Assert.Single(tracestates);

            Assert.Equal($"00/{activity.Context.TraceId}/{activity.Context.SpanId}/01", traceparents.Single());
            Assert.Equal("k1=v1,k2=v2", tracestates.Single());
#endif

            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                new BaggagePropagator(),
            }));
        }

        [Fact]
        public async Task RespectsSuppress()
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

                using var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(this.url),
                    Method = new HttpMethod("GET"),
                };

                using var parent = new Activity("parent")
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
                        await c.SendAsync(request).ConfigureAwait(false);
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
        public async Task ExportsSpansCreatedForRetries()
        {
            var exportedItems = new List<Activity>();
            using var request = new HttpRequestMessage
            {
                RequestUri = new Uri(this.url),
                Method = new HttpMethod("GET"),
            };

            using var traceprovider = Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();

            int maxRetries = 3;
            using var clientHandler = new HttpClientHandler();
            using var retryHandler = new RetryHandler(clientHandler, maxRetries);
            using var httpClient = new HttpClient(retryHandler);
            await httpClient.SendAsync(request).ConfigureAwait(false);

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
        public async Task RedirectTest()
        {
            var processor = new Mock<BaseProcessor<Activity>>();
            using (Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddProcessor(processor.Object)
                .Build())
            {
                using var c = new HttpClient();
                await c.GetAsync($"{this.url}redirect").ConfigureAwait(false);
            }

#if NETFRAMEWORK
            // Note: HttpWebRequest automatically handles redirects and reuses
            // the same instance which is patched reflectively. There isn't a
            // good way to produce two spans when redirecting that we have
            // found. For now, this is not supported.

            Assert.Equal(5, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnShutdown/Dispose called.

            var firstActivity = (Activity)processor.Invocations[2].Arguments[0]; // First OnEnd
            Assert.Contains(firstActivity.TagObjects, t => t.Key == "http.status_code" && (int)t.Value == 200);
#else
            Assert.Equal(7, processor.Invocations.Count); // SetParentProvider/OnStart/OnEnd/OnStart/OnEnd/OnShutdown/Dispose called.

            var firstActivity = (Activity)processor.Invocations[2].Arguments[0]; // First OnEnd
            Assert.Contains(firstActivity.TagObjects, t => t.Key == "http.status_code" && (int)t.Value == 302);

            var secondActivity = (Activity)processor.Invocations[4].Arguments[0]; // Second OnEnd
            Assert.Contains(secondActivity.TagObjects, t => t.Key == "http.status_code" && (int)t.Value == 200);
#endif
        }

        [Fact]
        public async void RequestNotCollectedWhenInstrumentationFilterApplied()
        {
            var exportedItems = new List<Activity>();

            bool httpWebRequestFilterApplied = false;
            bool httpRequestMessageFilterApplied = false;

            using (Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation(
                    opt =>
                    {
                        opt.FilterHttpWebRequest = (req) =>
                        {
                            httpWebRequestFilterApplied = true;
                            return !req.RequestUri.OriginalString.Contains(this.url);
                        };
                        opt.FilterHttpRequestMessage = (req) =>
                        {
                            httpRequestMessageFilterApplied = true;
                            return !req.RequestUri.OriginalString.Contains(this.url);
                        };
                    })
                .AddInMemoryExporter(exportedItems)
                .Build())
            {
                using var c = new HttpClient();
                await c.GetAsync(this.url).ConfigureAwait(false);
            }

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
        public async void RequestNotCollectedWhenInstrumentationFilterThrowsException()
        {
            var exportedItems = new List<Activity>();

            using (Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation(
                    (opt) =>
                    {
                        opt.FilterHttpWebRequest = (req) => throw new Exception("From InstrumentationFilter");
                        opt.FilterHttpRequestMessage = (req) => throw new Exception("From InstrumentationFilter");
                    })
                .AddInMemoryExporter(exportedItems)
                .Build())
            {
                using var c = new HttpClient();
                using var inMemoryEventListener = new InMemoryEventListener(HttpInstrumentationEventSource.Log);
                await c.GetAsync(this.url).ConfigureAwait(false);
                Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 4));
            }

            Assert.Empty(exportedItems);
        }

        [Fact]
        public async Task ReportsExceptionEventForNetworkFailuresWithGetAsync()
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
                await c.GetAsync("https://sdlfaldfjalkdfjlkajdflkajlsdjf.sdlkjafsdjfalfadslkf.com/").ConfigureAwait(false);
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
        public async Task DoesNotReportExceptionEventOnErrorResponseWithGetAsync()
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
                await c.GetAsync($"{this.url}500").ConfigureAwait(false);
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
        public async Task DoesNotReportExceptionEventOnErrorResponseWithGetStringAsync()
        {
            var exportedItems = new List<Activity>();
            bool exceptionThrown = false;
            using var request = new HttpRequestMessage
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
                await c.GetStringAsync($"{this.url}500").ConfigureAwait(false);
            }
            catch
            {
                exceptionThrown = true;
            }

            // Exception is thrown and not collected as event
            Assert.True(exceptionThrown);
            Assert.Empty(exportedItems[0].Events);
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

                    setter(carrier, "custom_traceparent", $"00/{contextFromPropagator.TraceId}/{contextFromPropagator.SpanId}/01");
                    setter(carrier, "custom_tracestate", contextFromPropagator.TraceState);
                });
#else
            propagator.Setup(m => m.Inject(It.IsAny<PropagationContext>(), It.IsAny<HttpRequestMessage>(), It.IsAny<Action<HttpRequestMessage, string, string>>()))
                .Callback<PropagationContext, HttpRequestMessage, Action<HttpRequestMessage, string, string>>((context, carrier, setter) =>
                {
                    contextFromPropagator = context.ActivityContext;

                    setter(carrier, "custom_traceparent", $"00/{contextFromPropagator.TraceId}/{contextFromPropagator.SpanId}/01");
                    setter(carrier, "custom_tracestate", contextFromPropagator.TraceState);
                });
#endif

            var exportedItems = new List<Activity>();

            using (var traceprovider = Sdk.CreateTracerProviderBuilder()
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

                using var request = new HttpRequestMessage
                {
                    RequestUri = new Uri(this.url),
                    Method = new HttpMethod("GET"),
                };

                using var c = new HttpClient();
                await c.SendAsync(request).ConfigureAwait(false);

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

            // Make sure custom propagator was called.
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

        public void Dispose()
        {
            this.serverLifeTime?.Dispose();
            Activity.Current = null;
            GC.SuppressFinalize(this);
        }
    }
}
