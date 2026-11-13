// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Tests;

[Collection(PropagatorsCollectionDefinition.Name)]
public class SdkTests
{
    [Fact]
    public void DefaultTextMapPropagatorIsCached()
    {
        var propagator = Sdk.DefaultTextMapPropagator;

        Assert.NotNull(propagator);
        Assert.IsType<CompositeTextMapPropagator>(propagator);
        Assert.Same(propagator, Sdk.DefaultTextMapPropagator);
    }

    [Fact]
    public void DefaultTextMapPropagatorIsInstalledAsTheGlobalDefault()
    {
        var propagator = Sdk.DefaultTextMapPropagator;

        Assert.Same(propagator, Propagators.DefaultTextMapPropagator);
    }

    [Fact]
    public void DefaultTextMapPropagatorPropagatesTraceContextAndBaggage()
    {
        var propagator = Sdk.DefaultTextMapPropagator;

        var fields = propagator.Fields;

        Assert.NotNull(fields);
        Assert.Contains("traceparent", fields);
        Assert.Contains("tracestate", fields);
        Assert.Contains("baggage", fields);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var activityContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, "key=value", isRemote: true);
        var baggage = Baggage.Create(new Dictionary<string, string> { ["name"] = "value" });

        var carrier = new Dictionary<string, string>();
        propagator.Inject(new PropagationContext(activityContext, baggage), carrier, (c, k, v) => c[k] = v);

        var extracted = propagator.Extract(default, carrier, (c, k) => c.TryGetValue(k, out var v) ? [v] : []);

        Assert.Equal(traceId, extracted.ActivityContext.TraceId);
        Assert.Equal(spanId, extracted.ActivityContext.SpanId);
        Assert.Equal("key=value", extracted.ActivityContext.TraceState);
        Assert.Equal("value", extracted.Baggage.GetBaggage("name"));
    }

    [Fact]
    public void DefaultTextMapPropagatorCanBeRestoredAfterCustomPropagatorIsSet()
    {
        var defaultPropagator = Sdk.DefaultTextMapPropagator;
        var original = Propagators.DefaultTextMapPropagator;
        var custom = new BaggagePropagator();

        try
        {
            Sdk.SetDefaultTextMapPropagator(custom);

            Assert.Same(custom, Propagators.DefaultTextMapPropagator);
            Assert.NotSame(defaultPropagator, Propagators.DefaultTextMapPropagator);
            Assert.Same(defaultPropagator, Sdk.DefaultTextMapPropagator);

            Sdk.SetDefaultTextMapPropagator(Sdk.DefaultTextMapPropagator);

            Assert.Same(defaultPropagator, Propagators.DefaultTextMapPropagator);
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(original);
        }
    }
}
