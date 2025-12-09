// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using OpenTracing.Tag;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

[Collection(nameof(ListenAndSampleAllActivitySources))]
public class SpanShimTests
{
    private const string SpanName = "MySpanName/1";
    private const string TracerName = "defaultactivitysource";

    [Fact]
    public void CtorArgumentValidation()
    {
        Assert.Throws<ArgumentNullException>(() => new SpanShim(null!));
    }

    [Fact]
    public void SpanContextIsNotNull()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        // ISpanContext validation handled in a separate test class
        Assert.NotNull(shim.Context);
    }

    [Fact]
    public void FinishSpan()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));
        shim.Finish();

        Assert.NotNull(shim.Span.Activity);
        Assert.NotEqual(default, shim.Span.Activity.Duration);
    }

    [Fact]
    public void FinishSpanUsingSpecificTimestamp()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

#if NETFRAMEWORK
        // Under the hood the Activity start time uses DateTime.UtcNow, which
        // doesn't have the same precision as DateTimeOffset.UtcNow on the .NET Framework.
        // Add a sleep big enough to ensure that the test doesn't break due to the
        // low resolution of DateTime.UtcNow on the .NET Framework.
        Thread.Sleep(TimeSpan.FromMilliseconds(20));
#endif

        var endTime = DateTimeOffset.UtcNow;
        shim.Finish(endTime);

        Assert.Equal(endTime - shim.Span.Activity!.StartTimeUtc, shim.Span.Activity.Duration);
    }

    [Fact]
    public void SetOperationName()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        // parameter validation
        Assert.Throws<ArgumentNullException>(() => shim.SetOperationName(null!));

        shim.SetOperationName("bar");
        Assert.Equal("bar", shim.Span.Activity!.DisplayName);
    }

    [Fact]
    public void GetBaggageItem()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        // parameter validation
        Assert.Throws<ArgumentException>(() => shim.GetBaggageItem(null!));

        // TODO - Method not implemented
    }

    [Fact]
    public void Log()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        shim.Log("foo");

        Assert.NotNull(shim.Span.Activity);
        var first = Assert.Single(shim.Span.Activity.Events);
        Assert.Equal("foo", first.Name);
        Assert.False(first.Tags.Any());
    }

    [Fact]
    public void LogWithExplicitTimestamp()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        var now = DateTimeOffset.UtcNow;
        shim.Log(now, "foo");

        Assert.NotNull(shim.Span.Activity);
        var first = Assert.Single(shim.Span.Activity.Events);
        Assert.Equal("foo", first.Name);
        Assert.Equal(now, first.Timestamp);
        Assert.False(first.Tags.Any());
    }

    [Fact]
    public void LogUsingFields()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.Log((IEnumerable<KeyValuePair<string, object>>)null!));

        shim.Log(new List<KeyValuePair<string, object>>
        {
            new("foo", "bar"),
        });

        // "event" is a special event name
        shim.Log(new List<KeyValuePair<string, object>>
        {
            new("event", "foo"),
        });

        Assert.NotNull(shim.Span.Activity);

        var first = shim.Span.Activity.Events.FirstOrDefault();
        var last = shim.Span.Activity.Events.LastOrDefault();

        Assert.Equal(2, shim.Span.Activity.Events.Count());

        Assert.Equal(SpanShim.DefaultEventName, first.Name);
        Assert.True(first.Tags.Any());

        Assert.Equal("foo", last.Name);
        Assert.False(last.Tags.Any());
    }

    [Fact]
    public void LogUsingFieldsWithExplicitTimestamp()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.Log((IEnumerable<KeyValuePair<string, object>>)null!));
        var now = DateTimeOffset.UtcNow;

        shim.Log(now, new List<KeyValuePair<string, object>>
        {
            new("foo", "bar"),
        });

        // "event" is a special event name
        shim.Log(now, new List<KeyValuePair<string, object>>
        {
            new("event", "foo"),
        });

        Assert.NotNull(shim.Span.Activity);
        Assert.Equal(2, shim.Span.Activity.Events.Count());
        var first = shim.Span.Activity.Events.First();
        var last = shim.Span.Activity.Events.Last();

        Assert.Equal(SpanShim.DefaultEventName, first.Name);
        Assert.True(first.Tags.Any());
        Assert.Equal(now, first.Timestamp);

        Assert.Equal("foo", last.Name);
        Assert.False(last.Tags.Any());
        Assert.Equal(now, last.Timestamp);
    }

    [Fact]
    public void SetTagStringValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null!, "foo"));

        shim.SetTag("foo", "bar");

        Assert.NotNull(shim.Span.Activity);
        var first = Assert.Single(shim.Span.Activity.Tags);
        Assert.Equal("foo", first.Key);
        Assert.Equal("bar", first.Value);
    }

    [Fact]
    public void SetTagBoolValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null!, true));

        shim.SetTag("foo", true);
        shim.SetTag(Tags.Error.Key, true);

        Assert.NotNull(shim.Span.Activity);
        var first = shim.Span.Activity.TagObjects.First();
        Assert.Equal("foo", first.Key);
        Assert.NotNull(first.Value);
        Assert.True((bool)first.Value);

        // A boolean tag named "error" is a special case that must be checked

        // Legacy span status tag should be set
        Assert.Equal("ERROR", shim.Span.Activity.GetTagValue(SpanAttributeConstants.StatusCodeKey));
        Assert.Equal(ActivityStatusCode.Error, shim.Span.Activity.Status);

        shim.SetTag(Tags.Error.Key, false);

        // Legacy span status tag should be set
        Assert.Equal("OK", shim.Span.Activity.GetTagValue(SpanAttributeConstants.StatusCodeKey));

        Assert.Equal(ActivityStatusCode.Ok, shim.Span.Activity.Status);
    }

    [Fact]
    public void SetTagIntValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.SetTag((string)null!, 1));

        shim.SetTag("foo", 1);

        Assert.NotNull(shim.Span.Activity);
        Assert.Single(shim.Span.Activity.TagObjects);
        Assert.Equal("foo", shim.Span.Activity.TagObjects.First().Key);
        Assert.Equal(1L, (int)shim.Span.Activity.TagObjects.First().Value!);
    }

    [Fact]
    public void SetTagDoubleValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.SetTag(null!, 1D));

        shim.SetTag("foo", 1D);

        Assert.NotNull(shim.Span.Activity);
        var first = Assert.Single(shim.Span.Activity.TagObjects);
        Assert.Equal("foo", first.Key);
        Assert.NotNull(first.Value);
        Assert.Equal(1, (double)first.Value);
    }

    [Fact]
    public void SetTagStringTagValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.SetTag((StringTag)null!, "foo"));

        shim.SetTag(new StringTag("foo"), "bar");

        Assert.NotNull(shim.Span.Activity);
        var first = Assert.Single(shim.Span.Activity.Tags);
        Assert.Equal("foo", first.Key);
        Assert.Equal("bar", first.Value);
    }

    [Fact]
    public void SetTagIntTagValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.SetTag((IntTag)null!, 1));

        shim.SetTag(new IntTag("foo"), 1);

        Assert.NotNull(shim.Span.Activity);
        var first = Assert.Single(shim.Span.Activity.TagObjects);
        Assert.Equal("foo", first.Key);
        Assert.NotNull(first.Value);
        Assert.Equal(1L, (int)first.Value);
    }

    [Fact]
    public void SetTagIntOrStringTagValue()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new SpanShim(tracer.StartSpan(SpanName));

        Assert.Throws<ArgumentNullException>(() => shim.SetTag((IntOrStringTag)null!, "foo"));

        shim.SetTag(new IntOrStringTag("foo"), 1);
        shim.SetTag(new IntOrStringTag("bar"), "baz");

        Assert.NotNull(shim.Span.Activity);
        Assert.Equal(2, shim.Span.Activity.TagObjects.Count());

        var first = shim.Span.Activity.TagObjects.First();
        Assert.Equal("foo", first.Key);
        Assert.NotNull(first.Value);
        Assert.Equal(1L, (int)first.Value);

        var second = shim.Span.Activity.TagObjects.Last();
        Assert.Equal("bar", second.Key);
        Assert.Equal("baz", second.Value);
    }
}
