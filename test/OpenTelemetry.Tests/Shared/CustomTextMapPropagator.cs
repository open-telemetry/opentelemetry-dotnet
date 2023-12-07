// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Tests;

internal sealed class CustomTextMapPropagator : TextMapPropagator
{
    private static readonly PropagationContext DefaultPropagationContext = default;

    public ActivityTraceId TraceId { get; set; }

    public ActivitySpanId SpanId { get; set; }

    public Action<PropagationContext> Injected { get; set; }

    public override ISet<string> Fields => null;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1010 // Opening square brackets should be spaced correctly
    public Dictionary<string, Func<PropagationContext, string>> InjectValues = [];
#pragma warning restore SA1010 // Opening square brackets should be spaced correctly
#pragma warning restore SA1201 // Elements should appear in the correct order

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
    {
        if (this.TraceId != default && this.SpanId != default)
        {
            return new PropagationContext(
                new ActivityContext(
                    this.TraceId,
                    this.SpanId,
                    ActivityTraceFlags.Recorded),
                default);
        }

        return DefaultPropagationContext;
    }

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        foreach (var kv in this.InjectValues)
        {
            setter(carrier, kv.Key, kv.Value.Invoke(context));
        }

        this.Injected?.Invoke(context);
    }
}