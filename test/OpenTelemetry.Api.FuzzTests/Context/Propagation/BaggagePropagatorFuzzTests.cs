// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
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
    public Property InjectExtractRoundTripPreservesSafeBaggage() => Prop.ForAll(Generators.SafeBaggageDictionaryArbitrary(), (baggageItems) =>
    {
        try
        {
            var propagator = new BaggagePropagator();
            var carrier = new Dictionary<string, string>(StringComparer.Ordinal);
            var propagationContext = new PropagationContext(default, Baggage.Create(baggageItems));

            propagator.Inject(propagationContext, carrier, FuzzTestHelpers.Setter);

            var extracted = propagator.Extract(default, carrier, FuzzTestHelpers.Getter);

            var normalized = Normalize(baggageItems);

            return DictionariesEqual(
                normalized,
                extracted.Baggage.GetBaggage());
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

    private static Dictionary<string, string> Normalize(Dictionary<string, string> input)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> kvp in input)
        {
            var key = kvp.Key;
            var value = kvp.Value;

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (!IsValidKey(key))
            {
                continue;
            }

#if NET || NETSTANDARD2_1_OR_GREATER
            var semicolonIndex = value.IndexOf(';', StringComparison.Ordinal);
#else
            var semicolonIndex = value.IndexOf(';');
#endif

            var truncated = semicolonIndex >= 0
                ? value.Substring(0, semicolonIndex)
                : value;

            truncated = truncated.Trim();

            var decoded = DecodeIfNeeded(truncated);

            if (string.IsNullOrEmpty(decoded))
            {
                continue;
            }

            result[key] = decoded;
        }

        return result;
    }

    private static string DecodeIfNeeded(string value)
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return value.Contains('%', StringComparison.Ordinal) == true
            ? WebUtility.UrlDecode(value)
            : value;
#else
        return value.Contains('%') == true
            ? WebUtility.UrlDecode(value)
            : value;
#endif
    }

    private static bool IsValidKey(string key)
    {
        foreach (var c in key)
        {
            // Control chars
            if (c <= 31 || c == 127)
            {
                return false;
            }

            // Delimiters disallowed in baggage keys
            switch (c)
            {
                case '(':
                case ')':
                case '<':
                case '>':
                case '@':
                case ',':
                case ';':
                case ':':
                case '\\':
                case '"':
                case '/':
                case '[':
                case ']':
                case '?':
                case '=':
                case '{':
                case '}':
                case ' ':
                case '\t':
                    return false;
            }
        }

        return true;
    }
}
