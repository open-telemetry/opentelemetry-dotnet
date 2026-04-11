// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Api.FuzzTests;

public class BaggagePropagatorFuzzTests
{
    private const string BaggageHeader = "baggage";
    private const int MaxTests = 200;

    [Property(MaxTest = MaxTests)]
    public Property InjectExtractRoundTripPreservesSafeBaggage() => Prop.ForAll(Generators.SafeBaggageDictionaryArbitrary(), (baggageItems) =>
    {
        try
        {
            var propagator = new BaggagePropagator();
            var carrier = new Dictionary<string, string>(StringComparer.Ordinal);
            var propagationContext = new PropagationContext(default, Baggage.Create(baggageItems));

            propagator.Inject(propagationContext, carrier, FuzzTestHelpers.Setter);

            var extracted = propagator.Extract(default, carrier, FuzzTestHelpers.Getter);

            return DictionariesEqual(baggageItems, extracted.Baggage.GetBaggage());
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property InjectedHeadersStayWithinConfiguredLimits() => Prop.ForAll(Generators.BaggageDictionaryArbitrary(), (baggageItems) =>
    {
        try
        {
            var propagator = new BaggagePropagator();
            var carrier = new Dictionary<string, string>(StringComparer.Ordinal);
            var propagationContext = new PropagationContext(default, Baggage.Create(baggageItems));

            propagator.Inject(propagationContext, carrier, FuzzTestHelpers.Setter);

            return
                !carrier.TryGetValue(BaggageHeader, out var headerValue) ||
                (headerValue.Length <= 8192 && headerValue.Split(',').Length <= 180);
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property ExtractIsDeterministicForArbitraryHeaders() => Prop.ForAll(Generators.BaggageCarrierArbitrary(), (carrier) =>
    {
        try
        {
            var propagator = new BaggagePropagator();
            var original = FuzzTestHelpers.CloneCarrier(carrier);

            var first = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);
            var second = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);

            return
                FuzzTestHelpers.CarriersEqual(original, carrier) &&
                DictionariesEqual(first.Baggage.GetBaggage(), second.Baggage.GetBaggage());
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    private static bool DictionariesEqual(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual) =>
        expected.Count == actual.Count &&
        expected.All(pair => actual.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
