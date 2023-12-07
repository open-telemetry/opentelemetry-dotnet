// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Instrumentation.Http.Implementation;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

#pragma warning disable SYSLIB0014 // Type or member is obsolete

namespace OpenTelemetry.Instrumentation.Http.Tests;

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
        var exportedItems = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddHttpClientInstrumentation()
            .Build();

        var request = (HttpWebRequest)WebRequest.Create(this.url);

        request.Method = "GET";

        request.Headers.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");

        using var response = await request.GetResponseAsync();

#if NETFRAMEWORK
        Assert.Empty(exportedItems);
#else
        Assert.Single(exportedItems);
#endif
    }

    [Fact]
    public async Task RequestNotCollectedWhenInstrumentationFilterApplied()
    {
        bool httpWebRequestFilterApplied = false;
        bool httpRequestMessageFilterApplied = false;

        var exportedItems = new List<Activity>();

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
        var exportedItems = new List<Activity>();

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
        var exportedItems = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .AddHttpClientInstrumentation()
            .Build();

        var request = (HttpWebRequest)WebRequest.Create(this.url);

        request.Method = "GET";

        using var parent = new Activity("parent")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        parent.TraceStateString = "k1=v1,k2=v2";
        parent.ActivityTraceFlags = ActivityTraceFlags.Recorded;

        using var response = await request.GetResponseAsync();

        Assert.Single(exportedItems);
        var activity = exportedItems[0];

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

        var propagator = new CustomTextMapPropagator
        {
            Injected = (PropagationContext context) => contextFromPropagator = context.ActivityContext,
        };
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

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<HttpClientTraceInstrumentationOptions>(name, o => configurationDelegateInvocations++);
            })
            .AddHttpClientInstrumentation(name, options =>
            {
                Assert.IsType<HttpClientTraceInstrumentationOptions>(options);
            })
            .Build();

        Assert.Equal(1, configurationDelegateInvocations);
    }

    [Fact]
    public async Task ReportsExceptionEventForNetworkFailures()
    {
        var exportedItems = new List<Activity>();
        bool exceptionThrown = false;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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
