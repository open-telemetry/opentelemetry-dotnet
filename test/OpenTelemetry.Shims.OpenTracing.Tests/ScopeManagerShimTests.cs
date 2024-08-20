// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

[Collection(nameof(ListenAndSampleAllActivitySources))]
public class ScopeManagerShimTests
{
    private const string SpanName = "MySpanName/1";
    private const string TracerName = "defaultactivitysource";

    [Fact]
    public void Active_IsNull()
    {
        var shim = new ScopeManagerShim();

        Assert.Null(Activity.Current);
        Assert.Null(shim.Active);
    }

    [Fact]
    public void Active_IsNotNull()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new ScopeManagerShim();
        var openTracingSpan = new SpanShim(tracer.StartSpan(SpanName));

        var scope = shim.Activate(openTracingSpan, true);
        Assert.NotNull(scope);

        var activeScope = shim.Active;
        Assert.Equal(scope.Span.Context.SpanId, activeScope!.Span.Context.SpanId);
        openTracingSpan.Finish();
    }

    [Fact]
    public void Activate_SpanMustBeShim()
    {
        var shim = new ScopeManagerShim();

        Assert.Throws<InvalidCastException>(() => shim.Activate(new TestSpan(), true));
    }

    [Fact]
    public void Activate()
    {
        var tracer = TracerProvider.Default.GetTracer(TracerName);
        var shim = new ScopeManagerShim();
        var spanShim = new SpanShim(tracer.StartSpan(SpanName));

        using (shim.Activate(spanShim, true))
        {
#if DEBUG
            Assert.Equal(1, shim.SpanScopeTableCount);
#endif
        }

#if DEBUG
        Assert.Equal(0, shim.SpanScopeTableCount);
#endif

        spanShim.Finish();
        Assert.NotEqual(default, spanShim.Span.Activity!.Duration);
    }
}
