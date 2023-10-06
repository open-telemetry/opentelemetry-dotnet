// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class TracerTest : IDisposable
{
    // TODO: This is only a basic test. This must cover the entire shim API scenarios.
    private readonly Tracer tracer;

    public TracerTest()
    {
        this.tracer = TracerProvider.Default.GetTracer("tracername", "tracerversion");
    }

    [Fact]
    public void CurrentSpanNullByDefault()
    {
        var current = Tracer.CurrentSpan;
        Assert.True(IsNoopSpan(current));
        Assert.False(current.Context.IsValid);
    }

    [Fact]
    public void TracerStartWithSpan()
    {
        Tracer.WithSpan(TelemetrySpan.NoopInstance);
        var current = Tracer.CurrentSpan;
        Assert.Same(current, TelemetrySpan.NoopInstance);
    }

    [Fact]
    public void TracerStartReturnsNoopSpanWhenNoSdk()
    {
        var span = this.tracer.StartSpan("name");
        Assert.True(IsNoopSpan(span));
        Assert.False(span.Context.IsValid);
        Assert.False(span.IsRecording);
    }

    [Fact]
    public void Tracer_StartRootSpan_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartRootSpan(null);
        Assert.Null(span1.Activity.DisplayName);

        var span2 = this.tracer.StartRootSpan(null, SpanKind.Client);
        Assert.Null(span2.Activity.DisplayName);

        var span3 = this.tracer.StartRootSpan(null, SpanKind.Client, default);
        Assert.Null(span3.Activity.DisplayName);
    }

    [Fact(Skip = "See https://github.com/open-telemetry/opentelemetry-dotnet/issues/2803")]
    public async Task Tracer_StartRootSpan_StartsNewTrace()
    {
        var exportedItems = new List<Activity>();

        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .AddInMemoryExporter(exportedItems)
            .Build();

        async Task DoSomeAsyncWork()
        {
            await Task.Delay(10).ConfigureAwait(false);
            using (tracer.GetTracer("tracername").StartRootSpan("RootSpan2"))
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        using (tracer.GetTracer("tracername").StartActiveSpan("RootSpan1"))
        {
            await DoSomeAsyncWork().ConfigureAwait(false);
        }

        Assert.Equal(2, exportedItems.Count);

        var rootSpan2 = exportedItems[0];
        var rootSpan1 = exportedItems[1];
        Assert.Equal("RootSpan2", rootSpan2.DisplayName);
        Assert.Equal("RootSpan1", rootSpan1.DisplayName);
        Assert.Equal(default, rootSpan1.ParentSpanId);

        // This is where this test currently fails
        // rootSpan2 should be a root span of a new trace and not a child of rootSpan1
        Assert.Equal(default, rootSpan2.ParentSpanId);
        Assert.NotEqual(rootSpan2.TraceId, rootSpan1.TraceId);
        Assert.NotEqual(rootSpan2.ParentSpanId, rootSpan1.SpanId);
    }

    [Fact]
    public void Tracer_StartSpan_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartSpan(null);
        Assert.Null(span1.Activity.DisplayName);

        var span2 = this.tracer.StartSpan(null, SpanKind.Client);
        Assert.Null(span2.Activity.DisplayName);

        var span3 = this.tracer.StartSpan(null, SpanKind.Client, null);
        Assert.Null(span3.Activity.DisplayName);
    }

    [Fact]
    public void Tracer_StartActiveSpan_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartActiveSpan(null);
        Assert.Null(span1.Activity.DisplayName);

        var span2 = this.tracer.StartActiveSpan(null, SpanKind.Client);
        Assert.Null(span2.Activity.DisplayName);

        var span3 = this.tracer.StartActiveSpan(null, SpanKind.Client, null);
        Assert.Null(span3.Activity.DisplayName);
    }

    [Fact]
    public void Tracer_StartSpan_FromParent_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartSpan(null, SpanKind.Client, TelemetrySpan.NoopInstance);
        Assert.Null(span1.Activity.DisplayName);

        var span2 = this.tracer.StartSpan(null, SpanKind.Client, TelemetrySpan.NoopInstance, default);
        Assert.Null(span2.Activity.DisplayName);
    }

    [Fact]
    public void Tracer_StartSpan_FromParentContext_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var blankContext = default(SpanContext);

        var span1 = this.tracer.StartSpan(null, SpanKind.Client, blankContext);
        Assert.Null(span1.Activity.DisplayName);

        var span2 = this.tracer.StartSpan(null, SpanKind.Client, blankContext, default);
        Assert.Null(span2.Activity.DisplayName);
    }

    [Fact]
    public void Tracer_StartActiveSpan_FromParent_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartActiveSpan(null, SpanKind.Client, TelemetrySpan.NoopInstance);
        Assert.Null(span1.Activity.DisplayName);

        var span2 = this.tracer.StartActiveSpan(null, SpanKind.Client, TelemetrySpan.NoopInstance, default);
        Assert.Null(span2.Activity.DisplayName);
    }

    [Fact]
    public void Tracer_StartActiveSpan_FromParentContext_BadArgs_NullSpanName()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var blankContext = default(SpanContext);

        var span1 = this.tracer.StartActiveSpan(null, SpanKind.Client, blankContext);
        Assert.Null(span1.Activity.DisplayName);

        var span2 = this.tracer.StartActiveSpan(null, SpanKind.Client, blankContext, default);
        Assert.Null(span2.Activity.DisplayName);
    }

    [Fact]
    public void Tracer_StartActiveSpan_CreatesActiveSpan()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span1 = this.tracer.StartActiveSpan("Test");
        Assert.Equal(span1.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

        var span2 = this.tracer.StartActiveSpan("Test", SpanKind.Client);
        Assert.Equal(span2.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

        var span = this.tracer.StartSpan("foo");
        Tracer.WithSpan(span);

        var span3 = this.tracer.StartActiveSpan("Test", SpanKind.Client, span);
        Assert.Equal(span3.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);

        var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var span4 = this.tracer.StartActiveSpan("Test", SpanKind.Client, spanContext);
        Assert.Equal(span4.Activity.SpanId, Tracer.CurrentSpan.Context.SpanId);
    }

    [Fact]
    public void GetCurrentSpanBlank()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();
        Assert.False(Tracer.CurrentSpan.Context.IsValid);
    }

    [Fact]
    public void GetCurrentSpanBlankWontThrowOnEnd()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();
        var current = Tracer.CurrentSpan;
        current.End();
    }

    [Fact]
    public void GetCurrentSpan()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();

        var span = this.tracer.StartSpan("foo");
        Tracer.WithSpan(span);

        Assert.Equal(span.Context.SpanId, Tracer.CurrentSpan.Context.SpanId);
        Assert.True(Tracer.CurrentSpan.Context.IsValid);
    }

    [Fact]
    public void CreateSpan_Sampled()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .Build();
        var span = this.tracer.StartSpan("foo");
        Assert.True(span.IsRecording);
    }

    [Fact]
    public void CreateSpan_NotSampled()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("tracername")
            .SetSampler(new AlwaysOffSampler())
            .Build();

        var span = this.tracer.StartSpan("foo");
        Assert.False(span.IsRecording);
    }

    public void Dispose()
    {
        Activity.Current = null;
        GC.SuppressFinalize(this);
    }

    private static bool IsNoopSpan(TelemetrySpan span)
    {
        return span.Activity == null;
    }
}
