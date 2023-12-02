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
using System.Net.Http;
#endif
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Instrumentation.Http.Tests;

public partial class HttpClientTests : IDisposable
{
    private readonly ITestOutputHelper output;
    private readonly IDisposable serverLifeTime;
    private readonly string host;
    private readonly int port;
    private readonly string url;

    public HttpClientTests(ITestOutputHelper output)
    {
        this.output = output;

        this.serverLifeTime = TestHttpServer.RunServer(
            (ctx) =>
            {
                string traceparent = ctx.Request.Headers["traceparent"];
                string custom_traceparent = ctx.Request.Headers["custom_traceparent"];
                if ((ctx.Request.Headers["contextRequired"] == null
                    || bool.Parse(ctx.Request.Headers["contextRequired"]))
                    &&
                    (string.IsNullOrWhiteSpace(traceparent)
                        && string.IsNullOrWhiteSpace(custom_traceparent)))
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
                else if (ctx.Request.Headers["responseCode"] != null)
                {
                    ctx.Response.StatusCode = int.Parse(ctx.Request.Headers["responseCode"]);
                }
                else
                {
                    ctx.Response.StatusCode = 200;
                }

                ctx.Response.OutputStream.Close();
            },
            out var host,
            out var port);

        this.host = host;
        this.port = port;
        this.url = $"http://{host}:{port}/";

        this.output.WriteLine($"HttpServer started: {this.url}");
    }

    [Fact]
    public void AddHttpClientInstrumentation_NamedOptions()
    {
        int defaultExporterOptionsConfigureOptionsInvocations = 0;
        int namedExporterOptionsConfigureOptionsInvocations = 0;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<HttpClientTraceInstrumentationOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                services.Configure<HttpClientTraceInstrumentationOptions>("Instrumentation2", o => namedExporterOptionsConfigureOptionsInvocations++);
            })
            .AddHttpClientInstrumentation()
            .AddHttpClientInstrumentation("Instrumentation2", configureHttpClientTraceInstrumentationOptions: null)
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
        var exportedItems = new List<Activity>();

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
            .AddInMemoryExporter(exportedItems)
            .Build())
        {
            using var c = new HttpClient();
            await c.SendAsync(request);
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

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
        if (shouldEnrich)
        {
            Assert.Equal("yes", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpWebRequest").FirstOrDefault().Value);
            Assert.Equal("yes", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpWebResponse").FirstOrDefault().Value);
        }
        else
        {
            Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enrichedWithHttpWebRequest");
            Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enrichedWithHttpWebResponse");
        }

        Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enrichedWithHttpRequestMessage");
        Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enrichedWithHttpResponseMessage");
#else
        Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enrichedWithHttpWebRequest");
        Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enrichedWithHttpWebResponse");

        if (shouldEnrich)
        {
            Assert.Equal("yes", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpRequestMessage").FirstOrDefault().Value);
            Assert.Equal("yes", activity.Tags.Where(tag => tag.Key == "enrichedWithHttpResponseMessage").FirstOrDefault().Value);
        }
        else
        {
            Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enrichedWithHttpRequestMessage");
            Assert.DoesNotContain(activity.Tags, tag => tag.Key == "enrichedWithHttpResponseMessage");
        }
#endif
    }

    [Fact]
    public async Task InjectsHeadersAsync_CustomFormat()
    {
        var propagator = new CustomTextMapPropagator();
        propagator.InjectValues.Add("custom_traceParent", context => $"00/{context.ActivityContext.TraceId}/{context.ActivityContext.SpanId}/01");
        propagator.InjectValues.Add("custom_traceState", context => Activity.Current.TraceStateString);

        var exportedItems = new List<Activity>();

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

        Sdk.SetDefaultTextMapPropagator(propagator);

        using (Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build())
        {
            using var c = new HttpClient();
            await c.SendAsync(request);
        }

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

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
        Assert.True(request.Headers.TryGetValues("custom_traceParent", out var traceParents));
        Assert.True(request.Headers.TryGetValues("custom_traceState", out var traceStates));
        Assert.Single(traceParents);
        Assert.Single(traceStates);

        Assert.Equal($"00/{activity.Context.TraceId}/{activity.Context.SpanId}/01", traceParents.Single());
        Assert.Equal("k1=v1,k2=v2", traceStates.Single());
#endif

        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
        {
            new TraceContextPropagator(),
            new BaggagePropagator(),
        }));
    }

    [Fact(Skip = "https://github.com/open-telemetry/opentelemetry-dotnet/issues/5092")]
    public async Task RespectsSuppress()
    {
        try
        {
            var propagator = new CustomTextMapPropagator();
            propagator.InjectValues.Add("custom_traceParent", context => $"00/{context.ActivityContext.TraceId}/{context.ActivityContext.SpanId}/01");
            propagator.InjectValues.Add("custom_traceState", context => Activity.Current.TraceStateString);

            var exportedItems = new List<Activity>();

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

            Sdk.SetDefaultTextMapPropagator(propagator);

            using (Sdk.CreateTracerProviderBuilder()
                .AddHttpClientInstrumentation()
                .AddInMemoryExporter(exportedItems)
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
            Assert.Empty(exportedItems);
            Assert.False(request.Headers.Contains("custom_traceParent"));
            Assert.False(request.Headers.Contains("custom_traceState"));
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

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        int maxRetries = 3;
        using var clientHandler = new HttpClientHandler();
        using var retryHandler = new RetryHandler(clientHandler, maxRetries);
        using var httpClient = new HttpClient(retryHandler);
        await httpClient.SendAsync(request);

        // number of exported spans should be 3(maxRetries)
        Assert.Equal(maxRetries, exportedItems.Count);

        var spanid1 = exportedItems[0].SpanId;
        var spanid2 = exportedItems[1].SpanId;
        var spanid3 = exportedItems[2].SpanId;

        // Validate span ids are different
        Assert.NotEqual(spanid1, spanid2);
        Assert.NotEqual(spanid3, spanid1);
        Assert.NotEqual(spanid2, spanid3);
    }

    [Theory]
    [InlineData("CONNECT", "CONNECT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("GET", "GET")]
    [InlineData("PUT", "PUT")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("OPTIONS", "OPTIONS")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("Get", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("TRACE", "TRACE")]
    [InlineData("CUSTOM", "_OTHER")]
    public async Task HttpRequestMethodIsSetOnActivityAsPerSpec(string originalMethod, string expectedMethod)
    {
        var exportedItems = new List<Activity>();
        using var request = new HttpRequestMessage
        {
            RequestUri = new Uri(this.url),
            Method = new HttpMethod(originalMethod),
        };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var httpClient = new HttpClient();

        try
        {
            await httpClient.SendAsync(request);
        }
        catch
        {
            // ignore error.
        }

        Assert.Single(exportedItems);

        var activity = exportedItems[0];

        Assert.Contains(activity.TagObjects, t => t.Key == SemanticConventions.AttributeHttpRequestMethod);

        if (originalMethod.Equals(expectedMethod, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(expectedMethod, activity.DisplayName);
            Assert.DoesNotContain(activity.TagObjects, t => t.Key == SemanticConventions.AttributeHttpRequestMethodOriginal);
        }
        else
        {
            Assert.Equal("HTTP", activity.DisplayName);
            Assert.Equal(originalMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethodOriginal) as string);
        }

        Assert.Equal(expectedMethod, activity.GetTagValue(SemanticConventions.AttributeHttpRequestMethod) as string);
    }

    [Theory]
    [InlineData("CONNECT", "CONNECT")]
    [InlineData("DELETE", "DELETE")]
    [InlineData("GET", "GET")]
    [InlineData("PUT", "PUT")]
    [InlineData("HEAD", "HEAD")]
    [InlineData("OPTIONS", "OPTIONS")]
    [InlineData("PATCH", "PATCH")]
    [InlineData("Get", "GET")]
    [InlineData("POST", "POST")]
    [InlineData("TRACE", "TRACE")]
    [InlineData("CUSTOM", "_OTHER")]
    public async Task HttpRequestMethodIsSetonRequestDurationMetricAsPerSpec(string originalMethod, string expectedMethod)
    {
        var metricItems = new List<Metric>();
        using var request = new HttpRequestMessage
        {
            RequestUri = new Uri(this.url),
            Method = new HttpMethod(originalMethod),
        };

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddInMemoryExporter(metricItems)
            .Build();

        using var httpClient = new HttpClient();

        try
        {
            await httpClient.SendAsync(request);
        }
        catch
        {
            // ignore error.
        }

        meterProvider.Dispose();

        var metric = metricItems.FirstOrDefault(m => m.Name == "http.client.request.duration");

        Assert.NotNull(metric);

        var metricPoints = new List<MetricPoint>();
        foreach (var p in metric.GetMetricPoints())
        {
            metricPoints.Add(p);
        }

        Assert.Single(metricPoints);
        var mp = metricPoints[0];

        // Inspect Metric Attributes
        var attributes = new Dictionary<string, object>();
        foreach (var tag in mp.Tags)
        {
            attributes[tag.Key] = tag.Value;
        }

        Assert.Contains(attributes, kvp => kvp.Key == SemanticConventions.AttributeHttpRequestMethod && kvp.Value.ToString() == expectedMethod);

        Assert.DoesNotContain(attributes, t => t.Key == SemanticConventions.AttributeHttpRequestMethodOriginal);
    }

    [Fact]
    public async Task RedirectTest()
    {
        var exportedItems = new List<Activity>();
        using (Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddInMemoryExporter(exportedItems)
            .Build())
        {
            using var c = new HttpClient();
            await c.GetAsync($"{this.url}redirect");
        }

#if NETFRAMEWORK
        // Note: HttpWebRequest automatically handles redirects and reuses
        // the same instance which is patched reflectively. There isn't a
        // good way to produce two spans when redirecting that we have
        // found. For now, this is not supported.

        Assert.Single(exportedItems);
        Assert.Contains(exportedItems[0].TagObjects, t => t.Key == "http.response.status_code" && (int)t.Value == 200);
#else
        Assert.Equal(2, exportedItems.Count);
        Assert.Contains(exportedItems[0].TagObjects, t => t.Key == "http.response.status_code" && (int)t.Value == 302);
        Assert.Contains(exportedItems[1].TagObjects, t => t.Key == "http.response.status_code" && (int)t.Value == 200);
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
            await c.GetAsync(this.url);
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
            await c.GetAsync(this.url);
            Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 4));
        }

        Assert.Empty(exportedItems);
    }

    [Fact]
    public async Task ReportsExceptionEventForNetworkFailuresWithGetAsync()
    {
        var exportedItems = new List<Activity>();
        bool exceptionThrown = false;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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
    public async Task DoesNotReportExceptionEventOnErrorResponseWithGetAsync()
    {
        var exportedItems = new List<Activity>();
        bool exceptionThrown = false;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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
    public async Task DoesNotReportExceptionEventOnErrorResponseWithGetStringAsync()
    {
        var exportedItems = new List<Activity>();
        bool exceptionThrown = false;
        using var request = new HttpRequestMessage
        {
            RequestUri = new Uri($"{this.url}500"),
            Method = new HttpMethod("GET"),
        };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task CustomPropagatorCalled(bool sample, bool createParentActivity)
    {
        ActivityContext parentContext = default;
        ActivityContext contextFromPropagator = default;

        void Propagator_Injected(object sender, PropagationContextEventArgs e)
        {
            contextFromPropagator = e.Context.ActivityContext;
        }

        var propagator = new CustomTextMapPropagator();
        propagator.Injected += Propagator_Injected;
        propagator.InjectValues.Add("custom_traceParent", context => $"00/{context.ActivityContext.TraceId}/{context.ActivityContext.SpanId}/01");
        propagator.InjectValues.Add("custom_traceState", context => Activity.Current.TraceStateString);

        var exportedItems = new List<Activity>();

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
           .AddHttpClientInstrumentation()
           .AddInMemoryExporter(exportedItems)
           .SetSampler(sample ? new ParentBasedSampler(new AlwaysOnSampler()) : new AlwaysOffSampler())
           .Build())
        {
            var previousDefaultTextMapPropagator = Propagators.DefaultTextMapPropagator;
            Sdk.SetDefaultTextMapPropagator(propagator);

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
            await c.SendAsync(request);

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
        this.output.WriteLine($"HttpServer stopped: {this.url}");
        Activity.Current = null;
        GC.SuppressFinalize(this);
    }
}
