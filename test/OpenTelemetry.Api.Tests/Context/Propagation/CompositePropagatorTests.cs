// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests;

public class CompositePropagatorTests
{
    private static readonly string[] Empty = Array.Empty<string>();
    private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter = (headers, name) =>
    {
        count++;
        if (headers.TryGetValue(name, out var value))
        {
            return [value];
        }

        return Empty;
    };

    private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
    {
        carrier[name] = value;
    };

    private static int count = 0;

    private readonly ActivityTraceId traceId = ActivityTraceId.CreateRandom();
    private readonly ActivitySpanId spanId = ActivitySpanId.CreateRandom();

    [Fact]
    public void CompositePropagator_NullTextMapPropagators()
    {
        Assert.Throws<ArgumentNullException>(() => new CompositeTextMapPropagator(null!));
    }

    [Fact]
    public void CompositePropagator_EmptyTextMapPropagators()
    {
        var compositePropagator = new CompositeTextMapPropagator([]);
        Assert.Empty(compositePropagator.Fields);
    }

    [Fact]
    public void CompositePropagator_NullTextMapPropagator()
    {
        var compositePropagator = new CompositeTextMapPropagator([null!]);
        Assert.Empty(compositePropagator.Fields);
    }

    [Fact]
    public void CompositePropagator_NoOpTextMapPropagators()
    {
        var compositePropagator = new CompositeTextMapPropagator([new NoopTextMapPropagator()]);
        Assert.Empty(compositePropagator.Fields);
    }

    [Fact]
    public void CompositePropagator_SingleTextMapPropagator()
    {
        var testPropagator = new TestPropagator("custom-traceparent-1", "custom-tracestate-1");

        var compositePropagator = new CompositeTextMapPropagator([testPropagator]);

        // We expect a new HashSet, with a copy of the values from the propagator.
        Assert.Equal(testPropagator.Fields, compositePropagator.Fields);
        Assert.NotSame(testPropagator.Fields, compositePropagator.Fields);
    }

    [Fact]
    public void CompositePropagator_TestPropagator()
    {
        var testPropagatorA = new TestPropagator("custom-traceparent-1", "custom-tracestate-1");
        var testPropagatorB = new TestPropagator("custom-traceparent-2", "custom-tracestate-2");

        var compositePropagator = new CompositeTextMapPropagator([testPropagatorA, testPropagatorB,]);

        var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
        var propagationContext = new PropagationContext(activityContext, default);
        var carrier = new Dictionary<string, string>();
        using var activity = new Activity("test");

        compositePropagator.Inject(propagationContext, carrier, Setter);
        Assert.Contains(carrier, kv => kv.Key == "custom-traceparent-1");
        Assert.Contains(carrier, kv => kv.Key == "custom-traceparent-2");

        Assert.Equal(testPropagatorA.Fields.Count + testPropagatorB.Fields.Count, compositePropagator.Fields.Count);
        Assert.Subset(compositePropagator.Fields, testPropagatorA.Fields);
        Assert.Subset(compositePropagator.Fields, testPropagatorB.Fields);

        Assert.Equal(1, testPropagatorA.InjectCount);
        Assert.Equal(1, testPropagatorB.InjectCount);

        compositePropagator.Extract(default, new Dictionary<string, string>(), Getter);

        Assert.Equal(1, testPropagatorA.ExtractCount);
        Assert.Equal(1, testPropagatorB.ExtractCount);
    }

    [Fact]
    public void CompositePropagator_UsingSameTag()
    {
        const string header01 = "custom-tracestate-01";
        const string header02 = "custom-tracestate-02";

        var testPropagatorA = new TestPropagator("custom-traceparent", header01, true);
        var testPropagatorB = new TestPropagator("custom-traceparent", header02);

        var compositePropagator = new CompositeTextMapPropagator([testPropagatorA, testPropagatorB,]);

        var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null);
        var propagationContext = new PropagationContext(activityContext, default);

        var carrier = new Dictionary<string, string>();

        compositePropagator.Inject(propagationContext, carrier, Setter);
        Assert.Contains(carrier, kv => kv.Key == "custom-traceparent");

        Assert.Equal(3, compositePropagator.Fields.Count);

        Assert.Equal(1, testPropagatorA.InjectCount);
        Assert.Equal(1, testPropagatorB.InjectCount);

        // checking if the latest propagator is the one with the data. So, it will replace the previous one.
        Assert.Equal($"00-{this.traceId}-{this.spanId}-{header02.Split('-').Last()}", carrier["custom-traceparent"]);

        // resetting counter
        count = 0;
        compositePropagator.Extract(default, carrier, Getter);

        // checking if we accessed only two times: header/headerstate options
        // if that's true, we skipped the first one since we have a logic to for the default result
        Assert.Equal(2, count);

        Assert.Equal(1, testPropagatorA.ExtractCount);
        Assert.Equal(1, testPropagatorB.ExtractCount);
    }

    [Fact]
    public void CompositePropagator_ActivityContext_Baggage()
    {
        var compositePropagator = new CompositeTextMapPropagator(new List<TextMapPropagator>
        {
            new TraceContextPropagator(),
            new BaggagePropagator(),
        });

        var activityContext = new ActivityContext(this.traceId, this.spanId, ActivityTraceFlags.Recorded, traceState: null, isRemote: true);
        var baggage = new Dictionary<string, string> { ["key1"] = "value1" };

        var propagationContextActivityOnly = new PropagationContext(activityContext, default);
        var propagationContextBaggageOnly = new PropagationContext(default, new Baggage(baggage));
        var propagationContextBoth = new PropagationContext(activityContext, new Baggage(baggage));

        var carrier = new Dictionary<string, string>();
        compositePropagator.Inject(propagationContextActivityOnly, carrier, Setter);
        var extractedContext = compositePropagator.Extract(default, carrier, Getter);
        Assert.Equal(propagationContextActivityOnly, extractedContext);

        carrier = new Dictionary<string, string>();
        compositePropagator.Inject(propagationContextBaggageOnly, carrier, Setter);
        extractedContext = compositePropagator.Extract(default, carrier, Getter);
        Assert.Equal(propagationContextBaggageOnly, extractedContext);

        carrier = new Dictionary<string, string>();
        compositePropagator.Inject(propagationContextBoth, carrier, Setter);
        extractedContext = compositePropagator.Extract(default, carrier, Getter);
        Assert.Equal(propagationContextBoth, extractedContext);
    }
}
