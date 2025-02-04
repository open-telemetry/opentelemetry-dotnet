// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

public class ScopeManagerShimNotRecordedTests
{
    [Fact]
    public void MultipleNotRecordedSpans_ReturnNoopInstance_DoNotThrow()
    {
        var tracer = TracerProvider.Default.GetTracer("TracerName");
        var scopeManagerShim = new ScopeManagerShim();
        var span1 = new SpanShim(tracer.StartSpan("Span-1"));
        var span2 = new SpanShim(tracer.StartSpan("Span-2"));

        var exception = Record.Exception(() =>
        {
            using var scope1 = scopeManagerShim.Activate(span1, true);
            using var scope2 = scopeManagerShim.Activate(span1, true);
        });

        Assert.Same(TelemetrySpan.NoopInstance, span1.Span);
        Assert.Same(TelemetrySpan.NoopInstance, span2.Span);
        Assert.False(span1.Span.IsRecording);
        Assert.False(span2.Span.IsRecording);
        Assert.Null(exception);
    }
}
