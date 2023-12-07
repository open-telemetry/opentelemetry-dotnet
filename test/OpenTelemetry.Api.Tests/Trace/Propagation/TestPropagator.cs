// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Context.Propagation.Tests;

public class TestPropagator : TextMapPropagator
{
    private readonly string idHeaderName;
    private readonly string stateHeaderName;
    private readonly bool defaultContext;

    public TestPropagator(string idHeaderName, string stateHeaderName, bool defaultContext = false)
    {
        this.idHeaderName = idHeaderName;
        this.stateHeaderName = stateHeaderName;
        this.defaultContext = defaultContext;
    }

    public override ISet<string> Fields => new HashSet<string>() { this.idHeaderName, this.stateHeaderName };

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
    {
        if (this.defaultContext)
        {
            return context;
        }

        IEnumerable<string> id = getter(carrier, this.idHeaderName);
        if (!id.Any())
        {
            return context;
        }

        var traceparentParsed = TraceContextPropagator.TryExtractTraceparent(id.First(), out var traceId, out var spanId, out var traceoptions);
        if (!traceparentParsed)
        {
            return context;
        }

        string tracestate = string.Empty;
        IEnumerable<string> tracestateCollection = getter(carrier, this.stateHeaderName);
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
        string headerNumber = this.stateHeaderName.Split('-').Last();

        var traceparent = string.Concat("00-", context.ActivityContext.TraceId.ToHexString(), "-", context.ActivityContext.SpanId.ToHexString());
        traceparent = string.Concat(traceparent, "-", headerNumber);

        setter(carrier, this.idHeaderName, traceparent);

        string tracestateStr = context.ActivityContext.TraceState;
        if (tracestateStr?.Length > 0)
        {
            setter(carrier, this.stateHeaderName, tracestateStr);
        }
    }
}
