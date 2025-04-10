// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Context.Propagation.Tests;

public class TestPropagator : TextMapPropagator
{
    private readonly string idHeaderName;
    private readonly string stateHeaderName;
    private readonly bool defaultContext;

    private int extractCount;
    private int injectCount;

    public TestPropagator(string idHeaderName, string stateHeaderName, bool defaultContext = false)
    {
        this.idHeaderName = idHeaderName;
        this.stateHeaderName = stateHeaderName;
        this.defaultContext = defaultContext;
    }

    public int ExtractCount => this.extractCount;

    public int InjectCount => this.injectCount;

    public override ISet<string> Fields => new HashSet<string>() { this.idHeaderName, this.stateHeaderName };

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        Interlocked.Increment(ref this.extractCount);

        if (this.defaultContext)
        {
            return context;
        }

        var id = getter(carrier, this.idHeaderName);
        if (id == null || !id.Any())
        {
            return context;
        }

        var traceparentParsed = TraceContextPropagator.TryExtractTraceparent(id.First(), out var traceId, out var spanId, out var traceoptions);
        if (!traceparentParsed)
        {
            return context;
        }

        var tracestate = string.Empty;
        var tracestateCollection = getter(carrier, this.stateHeaderName);
        if (tracestateCollection?.Any() ?? false)
        {
            TraceContextPropagator.TryExtractTracestate(tracestateCollection.ToArray(), out tracestate);
        }

        return new PropagationContext(
            new ActivityContext(traceId, spanId, traceoptions, tracestate),
            context.Baggage);
    }

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        Interlocked.Increment(ref this.injectCount);

        var headerNumber = this.stateHeaderName.Split('-').Last();

        var traceparent = string.Concat("00-", context.ActivityContext.TraceId.ToHexString(), "-", context.ActivityContext.SpanId.ToHexString());
        traceparent = string.Concat(traceparent, "-", headerNumber);

        setter(carrier, this.idHeaderName, traceparent);

        var tracestateStr = context.ActivityContext.TraceState;
        if (tracestateStr?.Length > 0)
        {
            setter(carrier, this.stateHeaderName, tracestateStr);
        }
    }
}
