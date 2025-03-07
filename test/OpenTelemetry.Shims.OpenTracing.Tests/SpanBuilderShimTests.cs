// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

[Collection(nameof(ListenAndSampleAllActivitySources))]
public class SpanBuilderShimTests
{
    private const string SpanName1 = "MySpanName/1";
    private const string SpanName2 = "MySpanName/2";
    private const string TracerName = "defaultactivitysource";

    [Fact]
    public void CtorArgumentValidation()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(null!, "foo"));
        Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(tracer, null!));
    }

    [Fact]
    public void IgnoreActiveSpan()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        // Add a parent. The shim requires that the ISpan implementation be a SpanShim
        shim.AsChildOf(new SpanShim(tracer.StartSpan(SpanName1)));

        // Set to Ignore
        shim.IgnoreActiveSpan();

        // build
        var spanShim = (SpanShim)shim.Start();
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal("foo", spanShim.Span.Activity.OperationName);
    }

    [Fact]
    public void StartWithExplicitTimestamp()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        var startTimestamp = DateTimeOffset.Now;
        shim.WithStartTimestamp(startTimestamp);

        // build
        var spanShim = (SpanShim)shim.Start();
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal(startTimestamp, spanShim.Span.Activity.StartTimeUtc);
    }

    [Fact]
    public void AsChildOf_WithNullSpan()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        // Add a null parent
        shim.AsChildOf((global::OpenTracing.ISpan?)null);

        // build
        var spanShim = (SpanShim)shim.Start();

        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal("foo", spanShim.Span.Activity.OperationName);
        Assert.Null(spanShim.Span.Activity.Parent);
    }

    [Fact]
    public void AsChildOf_WithSpan()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        // Add a parent.
        var span = new SpanShim(tracer.StartSpan(SpanName1));
        shim.AsChildOf(span);

        // build
        var spanShim = (SpanShim)shim.Start();

        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal("foo", spanShim.Span.Activity.OperationName);
        Assert.NotNull(spanShim.Span.Activity.ParentId);
    }

    [Fact]
    public void Start_ActivityOperationRootSpanChecks()
    {
        // Create an activity
        using var activity = new Activity("foo");
        activity.SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        // matching root operation name
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");
        var spanShim1 = (SpanShim)shim.Start();

        Assert.NotNull(spanShim1.Span.Activity);
        Assert.Equal("foo", spanShim1.Span.Activity.OperationName);

        // mis-matched root operation name
        shim = new SpanBuilderShim(tracer, "foo");
        var spanShim2 = (SpanShim)shim.Start();

        Assert.NotNull(spanShim2.Span.Activity);
        Assert.Equal("foo", spanShim2.Span.Activity.OperationName);
        Assert.Equal(spanShim1.Context.TraceId, spanShim2.Context.TraceId);
    }

    [Fact]
    public void AsChildOf_MultipleCallsWithSpan()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        // Multiple calls
        var span1 = new SpanShim(tracer.StartSpan(SpanName1));
        var span2 = new SpanShim(tracer.StartSpan(SpanName2));
        shim.AsChildOf(span1);
        shim.AsChildOf(span2);

        // build
        var spanShim = (SpanShim)shim.Start();

        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal("foo", spanShim.Span.Activity.OperationName);
        Assert.Contains(spanShim.Context.TraceId, spanShim.Span.Activity.TraceId.ToHexString(), StringComparison.Ordinal);

        // TODO: Check for multi level parenting
    }

    [Fact]
    public void AsChildOf_WithNullSpanContext()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        // Add a null parent
        shim.AsChildOf((global::OpenTracing.ISpanContext?)null);

        // build
        var spanShim = (SpanShim)shim.Start();

        // should be no parent.
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Null(spanShim.Span.Activity.Parent);
    }

    [Fact]
    public void AsChildOfWithSpanContext()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        // Add a parent
        var spanContext = SpanContextShimTests.GetSpanContextShim();
        _ = shim.AsChildOf(spanContext);

        // build
        var spanShim = (SpanShim)shim.Start();

        Assert.NotNull(spanShim.Span.Activity);
        Assert.NotNull(spanShim.Span.Activity.ParentId);
    }

    [Fact]
    public void AsChildOf_MultipleCallsWithSpanContext()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        // Multiple calls
        var spanContext1 = SpanContextShimTests.GetSpanContextShim();
        var spanContext2 = SpanContextShimTests.GetSpanContextShim();

        // Add parent context
        shim.AsChildOf(spanContext1);

        // Adds as link as parent context already exists
        shim.AsChildOf(spanContext2);

        // build
        var spanShim = (SpanShim)shim.Start();
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal("foo", spanShim.Span.Activity.OperationName);
        Assert.Contains(spanContext1.TraceId, spanShim.Span.Activity.ParentId, StringComparison.Ordinal);
        Assert.Equal(spanContext2.SpanId, spanShim.Span.Activity.Links.First().Context.SpanId.ToHexString());
    }

    [Fact]
    public void WithTag_KeyIsSpanKindStringValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        shim.WithTag(global::OpenTracing.Tag.Tags.SpanKind.Key, global::OpenTracing.Tag.Tags.SpanKindClient);

        // build
        var spanShim = (SpanShim)shim.Start();

        // Not an attribute
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Empty(spanShim.Span.Activity.Tags);
        Assert.Equal("foo", spanShim.Span.Activity.OperationName);
        Assert.Equal(ActivityKind.Client, spanShim.Span.Activity.Kind);
    }

    [Fact]
    public void WithTag_KeyIsErrorStringValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, "true");

        // build
        var spanShim = (SpanShim)shim.Start();

        // Legacy span status tag should be set
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal("ERROR", spanShim.Span.Activity.GetTagValue(SpanAttributeConstants.StatusCodeKey));

        if (VersionHelper.IsApiVersionGreaterThanOrEqualTo(1, 10))
        {
            // Activity status code should also be set
            Assert.Equal(ActivityStatusCode.Error, spanShim.Span.Activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Unset, spanShim.Span.Activity.Status);
        }
    }

    [Fact]
    public void WithTag_KeyIsNullStringValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        shim.WithTag((string)null!, "unused");

        // build
        var spanShim = (SpanShim)shim.Start();

        // Null key was ignored
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Empty(spanShim.Span.Activity.Tags);
    }

    [Fact]
    public void WithTag_ValueIsIgnoredWhenNull()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        shim.WithTag("foo", null);

        // build
        var spanShim = (SpanShim)shim.Start();

        // Null value was turned into string.empty
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Empty(spanShim.Span.Activity.TagObjects);
    }

    [Fact]
    public void WithTag_KeyIsErrorBoolValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, true);

        // build
        var spanShim = (SpanShim)shim.Start();

        // Legacy span status tag should be set
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal("ERROR", spanShim.Span.Activity.GetTagValue(SpanAttributeConstants.StatusCodeKey));
        if (VersionHelper.IsApiVersionGreaterThanOrEqualTo(1, 10))
        {
            // Activity status code should also be set
            Assert.Equal(ActivityStatusCode.Error, spanShim.Span.Activity.Status);
        }
        else
        {
            Assert.Equal(ActivityStatusCode.Unset, spanShim.Span.Activity.Status);
        }
    }

    [Fact]
    public void WithTag_VariousValueTypes()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        shim.WithTag("foo", "unused");
        shim.WithTag("bar", false);
        shim.WithTag("baz", 1);
        shim.WithTag("bizzle", 1D);
        shim.WithTag(new global::OpenTracing.Tag.BooleanTag("shnizzle"), true);
        shim.WithTag(new global::OpenTracing.Tag.IntOrStringTag("febrizzle"), "unused");
        shim.WithTag(new global::OpenTracing.Tag.StringTag("mobizzle"), "unused");

        // build
        var spanShim = (SpanShim)shim.Start();

        // Just verify the count
        Assert.NotNull(spanShim.Span.Activity);
        Assert.Equal(7, spanShim.Span.Activity.TagObjects.Count());
    }

    [Fact]
    public void Start()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanBuilderShim(tracer, "foo");

        // build
        var span = shim.Start() as SpanShim;

        // Just check the return value is a SpanShim and that the underlying OpenTelemetry Span.
        // There is nothing left to verify because the rest of the tests were already calling .Start() prior to verification.
        Assert.NotNull(span);
        Assert.NotNull(span.Span.Activity);
        Assert.Equal("foo", span.Span.Activity.OperationName);
    }

    [Fact]
    public void Start_UnderAspNetCoreInstrumentation()
    {
        // Simulate a span from AspNetCore instrumentation as parent.
        using var source = new ActivitySource("Microsoft.AspNetCore.Hosting.HttpRequestIn");
        using var parentSpan = source.StartActivity("OTelParent");
        Assert.NotNull(parentSpan);

        // Start the OpenTracing span.
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var builderShim = new SpanBuilderShim(tracer, "foo");
        var spanShim = builderShim.StartActive().Span as SpanShim;
        Assert.NotNull(spanShim);

        var telemetrySpan = spanShim.Span;
        Assert.NotNull(telemetrySpan.Activity);
        Assert.Same(telemetrySpan.Activity, Activity.Current);
        Assert.Same(parentSpan, telemetrySpan.Activity.Parent);

        // Dispose the spanShim.Span and ensure correct state for Activity.Current
        spanShim.Span.Dispose();

        Assert.Same(parentSpan, Activity.Current);
    }
}
