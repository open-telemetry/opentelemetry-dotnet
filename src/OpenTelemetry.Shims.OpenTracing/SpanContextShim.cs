// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTracing;

namespace OpenTelemetry.Shims.OpenTracing;

internal sealed class SpanContextShim : ISpanContext
{
    public SpanContextShim(in Trace.SpanContext spanContext)
    {
        this.SpanContext = spanContext;
    }

    public Trace.SpanContext SpanContext { get; private set; }

    /// <inheritdoc/>
    public string TraceId => this.SpanContext.TraceId.ToString();

    /// <inheritdoc/>
    public string SpanId => this.SpanContext.SpanId.ToString();

    public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        => Baggage.GetBaggage();
}
