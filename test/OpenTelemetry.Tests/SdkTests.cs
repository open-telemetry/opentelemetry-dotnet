// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Tests;

public class SdkTests
{
    [Fact]
    public void CreateDefaultTextMapPropagatorReturnsNewInstances()
    {
        var first = Sdk.CreateDefaultTextMapPropagator();
        var second = Sdk.CreateDefaultTextMapPropagator();

        Assert.NotSame(first, second);
        var fields = first.Fields;
        Assert.NotNull(fields);
        Assert.Equal(["baggage", "traceparent", "tracestate"], fields.OrderBy(field => field));
        Assert.Equal(first.Fields, second.Fields);
    }

    [Fact]
    public void CreateDefaultTextMapPropagatorPropagatesTraceContextAndBaggage()
    {
        var propagator = Sdk.CreateDefaultTextMapPropagator();
        var incoming = new Dictionary<string, string>
        {
            ["traceparent"] = "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01",
            ["tracestate"] = "vendor=value",
            ["baggage"] = "customer=example",
        };

        var context = propagator.Extract(default, incoming, (carrier, name) =>
            carrier.TryGetValue(name, out var value) ? [value] : []);

        Assert.Equal("0123456789abcdef0123456789abcdef", context.ActivityContext.TraceId.ToHexString());
        Assert.Equal("0123456789abcdef", context.ActivityContext.SpanId.ToHexString());
        Assert.Equal("vendor=value", context.ActivityContext.TraceState);
        var baggage = Assert.Single(context.Baggage.GetBaggage());
        Assert.Equal("customer", baggage.Key);
        Assert.Equal("example", baggage.Value);

        var outgoing = new Dictionary<string, string>();
        propagator.Inject(context, outgoing, (carrier, name, value) => carrier[name] = value);

        Assert.Equal(incoming, outgoing);
    }

    [Fact]
    public void CreateDefaultTextMapPropagatorCanRestoreDefaultsWithoutChangingCurrentPropagator()
    {
        _ = Sdk.SuppressInstrumentation;
        var original = Propagators.DefaultTextMapPropagator;

        try
        {
            var custom = new TraceContextPropagator();
            Sdk.SetDefaultTextMapPropagator(custom);

            var defaultPropagator = Sdk.CreateDefaultTextMapPropagator();
            Assert.Same(custom, Propagators.DefaultTextMapPropagator);

            Sdk.SetDefaultTextMapPropagator(defaultPropagator);

            Assert.Same(defaultPropagator, Propagators.DefaultTextMapPropagator);
            var fields = Propagators.DefaultTextMapPropagator.Fields;
            Assert.NotNull(fields);
            Assert.Equal(["baggage", "traceparent", "tracestate"], fields.OrderBy(field => field));
        }
        finally
        {
            Sdk.SetDefaultTextMapPropagator(original);
        }
    }
}
