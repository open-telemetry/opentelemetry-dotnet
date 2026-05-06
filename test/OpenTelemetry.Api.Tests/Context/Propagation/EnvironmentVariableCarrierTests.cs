// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests;

public class EnvironmentVariableCarrierTests
{
    [Theory]
    [InlineData("traceparent", "TRACEPARENT")]
    [InlineData("tracestate", "TRACESTATE")]
    [InlineData("otel.trace.id", "OTEL_TRACE_ID")]
    [InlineData("otel-baggage-key", "OTEL_BAGGAGE_KEY")]
    [InlineData("123trace", "_123TRACE")]
    [InlineData("M\u00f6j Baga\u017c", "M_J_BAGA_")]
    public void NormalizeKey_CompliesWithSpecification(string key, string expected)
        => Assert.Equal(expected, EnvironmentVariableCarrier.NormalizeKey(key));

    [Fact]
    public void NormalizeKey_ReturnsOriginalStringWhenAlreadyNormalized()
    {
        var key = new string("TRACEPARENT".ToCharArray());

        Assert.Same(key, EnvironmentVariableCarrier.NormalizeKey(key));
    }

    [Fact]
    public void Capture_NormalizesKeysAndPreservesValues()
    {
        var snapshot = EnvironmentVariableCarrier.Capture(
        [
            new("traceparent", "00-0af7651916cd43dd8448eb211c80319c-b9c7c989f97918e1-01"),
            new("tracestate", "key1=value1,key2=value2"),
            new("otel.trace.id", "value with spaces\tand\nnewlines"),
        ]);

        Assert.Equal(
            "00-0af7651916cd43dd8448eb211c80319c-b9c7c989f97918e1-01",
            EnvironmentVariableCarrier.Get(snapshot, "TRACEPARENT")!.Single());

        Assert.Equal(
            "key1=value1,key2=value2",
            EnvironmentVariableCarrier.Get(snapshot, "tracestate")!.Single());

        Assert.Equal(
            "value with spaces\tand\nnewlines",
            EnvironmentVariableCarrier.Get(snapshot, "otel.trace.id")!.Single());
    }

    [Fact]
    public void Capture_PreservesEmptyAndOpaqueValues()
    {
        var opaqueValue = "value with spaces\tand\u0000control\u0080";
        var snapshot = EnvironmentVariableCarrier.Capture(
        [
            new("empty.key", string.Empty),
            new("opaque-key", opaqueValue),
        ]);

        Assert.Equal(string.Empty, EnvironmentVariableCarrier.Get(snapshot, "empty.key")!.Single());
        Assert.Equal(opaqueValue, EnvironmentVariableCarrier.Get(snapshot, "opaque-key")!.Single());
    }

    [Fact]
    public void Capture_CreatesSnapshotOfProcessEnvironment()
    {
        var key = $"otel.traceparent.{Guid.NewGuid():N}";
        var normalizedKey = EnvironmentVariableCarrier.NormalizeKey(key);

        using (EnvironmentVariableScope.Create(normalizedKey, "value-before"))
        {
            var snapshot = EnvironmentVariableCarrier.CaptureFromCurrentProcess();

            using (EnvironmentVariableScope.Create(normalizedKey, "value-after"))
            {
                Assert.Equal("value-before", EnvironmentVariableCarrier.Get(snapshot, key)!.Single());
            }
        }
    }

    [Fact]
    public void Capture_DoesNotObserveSourceMutationsAfterCapture()
    {
        var source = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["traceparent"] = "value-before",
        };

        var snapshot = EnvironmentVariableCarrier.Capture(source);

        source["traceparent"] = "value-after";
        source["tracestate"] = "new-value";

        Assert.Equal("value-before", EnvironmentVariableCarrier.Get(snapshot, "traceparent")!.Single());
        Assert.Null(EnvironmentVariableCarrier.Get(snapshot, "tracestate"));
    }

    [Fact]
    public void Get_SupportsUnnormalizedCarrierKeys()
    {
        List<KeyValuePair<string, string?>> carrier =
        [
            new("traceparent", "value1"),
            new("otel.trace.id", "value2"),
            new("Baggage", "value3"),
        ];

        Assert.Equal("value1", EnvironmentVariableCarrier.Get(carrier, "TRACEPARENT")!.Single());
        Assert.Equal("value2", EnvironmentVariableCarrier.Get(carrier, "OTEL_TRACE_ID")!.Single());
        Assert.Equal("value3", EnvironmentVariableCarrier.Get(carrier, "baggage")!.Single());
        Assert.Null(EnvironmentVariableCarrier.Get(carrier, "missing"));
    }

    [Fact]
    public void Get_PreservesEmptyAndOpaqueValues()
    {
        List<KeyValuePair<string, string?>> carrier =
        [
            new("empty-key", string.Empty),
            new("opaque-key", "value with spaces\tand\u0000control\u0080"),
        ];

        Assert.Equal(string.Empty, EnvironmentVariableCarrier.Get(carrier, "empty-key")!.Single());
        Assert.Equal("value with spaces\tand\u0000control\u0080", EnvironmentVariableCarrier.Get(carrier, "opaque-key")!.Single());
    }

    [Fact]
    public void Get_SupportsLeadingDigitKeyNormalization()
    {
        List<KeyValuePair<string, string?>> carrier =
        [
            new("123trace", "value"),
        ];

        Assert.Equal("value", EnvironmentVariableCarrier.Get(carrier, "_123TRACE")!.Single());
    }

    [Fact]
    public void Get_IgnoresLeadingDigitCarrierKeyForNonDigitLookupKey()
    {
        List<KeyValuePair<string, string?>> carrier =
        [
            new("123trace", "value1"),
            new("traceparent", "value2"),
        ];

        Assert.Equal("value2", EnvironmentVariableCarrier.Get(carrier, "traceparent")!.Single());
    }

    [Fact]
    public void Get_SupportsDictionaryOnlyCarrierKeys()
    {
        var carrier = new DictionaryOnlyCarrier
        {
            ["TRACEPARENT"] = "value1",
            ["OTEL_TRACE_ID"] = "value2",
        };

        Assert.Equal("value1", EnvironmentVariableCarrier.Get(carrier, "traceparent")!.Single());
        Assert.Equal("value2", EnvironmentVariableCarrier.Get(carrier, "otel.trace.id")!.Single());
        Assert.Null(EnvironmentVariableCarrier.Get(carrier, "missing"));
    }

    [Fact]
    public void Set_NormalizesKeysAndPreservesValues()
    {
        var carrier = new Dictionary<string, string?>(StringComparer.Ordinal);

        EnvironmentVariableCarrier.Set(carrier, "traceparent", "value1");
        EnvironmentVariableCarrier.Set(carrier, "otel.trace.id", "value with spaces");
        EnvironmentVariableCarrier.Set(carrier, "123trace", "value3");

        Assert.Equal("value1", carrier["TRACEPARENT"]);
        Assert.Equal("value with spaces", carrier["OTEL_TRACE_ID"]);
        Assert.Equal("value3", carrier["_123TRACE"]);
    }

    [Fact]
    public void Set_PreservesEmptyAndOpaqueValues()
    {
        var carrier = new Dictionary<string, string?>(StringComparer.Ordinal);

        EnvironmentVariableCarrier.Set(carrier, "empty-key", string.Empty);
        EnvironmentVariableCarrier.Set(carrier, "opaque-key", "value with spaces\tand\u0000control\u0080");

        Assert.Equal(string.Empty, carrier["EMPTY_KEY"]);
        Assert.Equal("value with spaces\tand\u0000control\u0080", carrier["OPAQUE_KEY"]);
    }

    [Fact]
    public void Set_TargetsEnvironmentCopyWithoutMutatingProcessEnvironment()
    {
        var key = $"otel.traceparent.{Guid.NewGuid():N}";
        var normalizedKey = EnvironmentVariableCarrier.NormalizeKey(key);
        var originalValue = Environment.GetEnvironmentVariable(normalizedKey);

        using (EnvironmentVariableScope.Create(normalizedKey, "process-value"))
        {
            var childEnvironment = EnvironmentVariableCarrier.CaptureFromCurrentProcess()
                .ToDictionary((item) => item.Key, (item) => item.Value, StringComparer.Ordinal);

            EnvironmentVariableCarrier.Set(childEnvironment, key, "child-value");

            Assert.Equal("process-value", Environment.GetEnvironmentVariable(normalizedKey));
            Assert.Equal("child-value", childEnvironment[normalizedKey]);
        }
    }

    [Fact]
    public void TraceContextPropagator_RoundTripsThroughEnvironmentVariableCarrier()
    {
        var activityContext = new ActivityContext(
            ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c"),
            ActivitySpanId.CreateFromString("b9c7c989f97918e1"),
            ActivityTraceFlags.Recorded,
            "key1=value1,key2=value2");

        var carrier = new Dictionary<string, string?>(StringComparer.Ordinal);
        var propagationContext = new PropagationContext(activityContext, default);
        var propagator = new TraceContextPropagator();

        propagator.Inject(propagationContext, carrier, EnvironmentVariableCarrier.Set);

        var extracted = propagator.Extract(default, EnvironmentVariableCarrier.Capture(carrier), EnvironmentVariableCarrier.Get);

        Assert.Equal("00-0af7651916cd43dd8448eb211c80319c-b9c7c989f97918e1-01", carrier["TRACEPARENT"]);
        Assert.Equal("key1=value1,key2=value2", carrier["TRACESTATE"]);
        Assert.Equal(activityContext.TraceId, extracted.ActivityContext.TraceId);
        Assert.Equal(activityContext.SpanId, extracted.ActivityContext.SpanId);
        Assert.Equal(activityContext.TraceFlags, extracted.ActivityContext.TraceFlags);
        Assert.Equal(activityContext.TraceState, extracted.ActivityContext.TraceState);
        Assert.True(extracted.ActivityContext.IsRemote);
    }

    [Fact]
    public void BaggagePropagator_RoundTripsThroughEnvironmentVariableCarrier()
    {
        var baggage = Baggage.Create(new Dictionary<string, string>
        {
            ["key1"] = "value 1", // space is not a valid token char in keys refer to #7051
            ["key2"] = "value2",
        });

        var carrier = new Dictionary<string, string?>(StringComparer.Ordinal);
        var propagationContext = new PropagationContext(default, baggage);
        var propagator = new BaggagePropagator();

        propagator.Inject(propagationContext, carrier, EnvironmentVariableCarrier.Set);

        var extracted = propagator.Extract(default, EnvironmentVariableCarrier.Capture(carrier), EnvironmentVariableCarrier.Get);

        Assert.Equal("key1=value%201,key2=value2", carrier["BAGGAGE"]);
        AssertBaggageEqual(baggage.GetBaggage(), extracted.Baggage.GetBaggage());
    }

    [Fact]
    public void CompositePropagator_RoundTripsTraceContextAndBaggageThroughEnvironmentVariableCarrier()
    {
        var activityContext = new ActivityContext(
            ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c"),
            ActivitySpanId.CreateFromString("b9c7c989f97918e1"),
            ActivityTraceFlags.Recorded,
            "key1=value1,key2=value2");

        var baggage = Baggage.Create(new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2",
        });

        var carrier = new Dictionary<string, string?>(StringComparer.Ordinal);
        var propagator = new CompositeTextMapPropagator(
        [
            new TraceContextPropagator(),
            new BaggagePropagator(),
        ]);

        var propagationContext = new PropagationContext(activityContext, baggage);

        propagator.Inject(propagationContext, carrier, EnvironmentVariableCarrier.Set);

        var extracted = propagator.Extract(default, EnvironmentVariableCarrier.Capture(carrier), EnvironmentVariableCarrier.Get);

        Assert.Equal(activityContext.TraceId, extracted.ActivityContext.TraceId);
        Assert.Equal(activityContext.SpanId, extracted.ActivityContext.SpanId);
        Assert.Equal(activityContext.TraceFlags, extracted.ActivityContext.TraceFlags);
        Assert.Equal(activityContext.TraceState, extracted.ActivityContext.TraceState);
        AssertBaggageEqual(baggage.GetBaggage(), extracted.Baggage.GetBaggage());
    }

    private static void AssertBaggageEqual(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        foreach (var item in expected)
        {
            Assert.True(actual.TryGetValue(item.Key, out var value), $"Could not find key '{item.Key}'");
            Assert.Equal(item.Value, value);
        }

        Assert.Equal(expected.Count, actual.Count);
    }

    private sealed class DictionaryOnlyCarrier : IDictionary<string, string?>
    {
        private readonly Dictionary<string, string?> inner = new(StringComparer.Ordinal);

        public ICollection<string> Keys => this.inner.Keys;

        public ICollection<string?> Values => this.inner.Values;

        public int Count => this.inner.Count;

        public bool IsReadOnly => false;

        public string? this[string key]
        {
            get => this.inner[key];
            set => this.inner[key] = value;
        }

        public void Add(string key, string? value) => this.inner.Add(key, value);

        public void Add(KeyValuePair<string, string?> item) => ((IDictionary<string, string?>)this.inner).Add(item);

        public void Clear() => this.inner.Clear();

        public bool Contains(KeyValuePair<string, string?> item) => ((IDictionary<string, string?>)this.inner).Contains(item);

        public bool ContainsKey(string key) => this.inner.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, string?>[] array, int arrayIndex) => ((IDictionary<string, string?>)this.inner).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, string?>> GetEnumerator() => this.inner.GetEnumerator();

        public bool Remove(string key) => this.inner.Remove(key);

        public bool Remove(KeyValuePair<string, string?> item) => ((IDictionary<string, string?>)this.inner).Remove(item);

        public bool TryGetValue(string key, out string? value) => this.inner.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
