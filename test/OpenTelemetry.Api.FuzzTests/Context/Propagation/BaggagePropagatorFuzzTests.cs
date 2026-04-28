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
    private const int MaxBaggageLength = 8192;
    private const int MaxBaggageItems = 180;

    [Property(MaxTest = MaxTests)]
    public Property ExtractNeverThrowsOnArbitraryInput() => Prop.ForAll(Generators.BaggageCarrierArbitrary(), (carrier) =>
    {
        try
        {
            var propagator = new BaggagePropagator();
            propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);
            return true;
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
        catch
        {
            return false;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property InjectNeverThrowsOnArbitraryInput() => Prop.ForAll(Generators.BaggageDictionaryArbitrary(), (baggageItems) =>
    {
        try
        {
            var propagator = new BaggagePropagator();
            var carrier = new Dictionary<string, string>(StringComparer.Ordinal);
            var propagationContext = new PropagationContext(default, Baggage.Create(baggageItems));
            propagator.Inject(propagationContext, carrier, FuzzTestHelpers.Setter);
            return true;
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
        catch
        {
            return false;
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

    [Property(MaxTest = MaxTests)]
    public Property ExtractMatchesReplayableGetterForSinglePassEnumerables() => Prop.ForAll(Generators.BaggageCarrierArbitrary(), (carrier) =>
    {
        try
        {
            var propagator = new BaggagePropagator();
            var original = FuzzTestHelpers.CloneCarrier(carrier);

            var replayable = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);
            var singlePass = propagator.Extract(default, carrier, FuzzTestHelpers.SinglePassArrayGetter);

            return
                FuzzTestHelpers.CarriersEqual(original, carrier) &&
                DictionariesEqual(replayable.Baggage.GetBaggage(), singlePass.Baggage.GetBaggage());
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    [Property(MaxTest = MaxTests)]
    public Property OversizedExtractionHonorsConfiguredLimits() => Prop.ForAll(Generators.OversizedBaggageValuesArbitrary(), (values) =>
    {
        try
        {
            var propagator = new BaggagePropagator();
            var carrier = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [BaggageHeader] = [string.Join(",", values.Select((value, index) => $"k{index:D4}={value}"))],
            };

            var extracted = propagator.Extract(default, carrier, FuzzTestHelpers.ArrayGetter);

            return DictionariesEqual(ExpectedBaggageForOversizedHeader(values), extracted.Baggage.GetBaggage());
        }
        catch (Exception ex) when (FuzzTestHelpers.IsAllowedException(ex))
        {
            return true;
        }
    });

    private static bool DictionariesEqual(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual) =>
        expected.Count == actual.Count &&
        expected.All(pair => actual.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static Dictionary<string, string> ExpectedBaggageForOversizedHeader(string[] values)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        var baggageLength = -1;

        for (var i = 0; i < values.Length; i++)
        {
            var key = $"k{i:D4}";
            baggageLength += key.Length + values[i].Length + 2; // key, equals sign, value, and comma.

            if (baggageLength >= MaxBaggageLength || expected.Count >= MaxBaggageItems)
            {
                break;
            }

            expected[key] = values[i];
        }

        return expected;
    }
}
