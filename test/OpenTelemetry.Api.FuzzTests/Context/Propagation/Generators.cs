// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck;
using FsCheck.Fluent;

namespace OpenTelemetry.Api.FuzzTests;

internal static class Generators
{
    private static readonly Gen<char> LowerAlphaNumericChar = Gen.Elements("abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray());
    private static readonly Gen<char> TraceStateKeyChar = Gen.Elements("abcdefghijklmnopqrstuvwxyz0123456789_-*/".ToCharArray());
    private static readonly Gen<char> TraceStateValueChar = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&'*+-.^_`|~:/".ToCharArray());
    private static readonly Gen<char> BaggageChar = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -_./:!$&'()*+;@?=,".ToCharArray());
    private static readonly Gen<char> CompactBaggageValueChar = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-".ToCharArray());
    private static readonly Gen<char> HeaderValueChar = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_=,;: ./@".ToCharArray());

    public static Arbitrary<ActivityContext> ActivityContextArbitrary()
    {
        var gen =
            from traceFlags in Gen.Elements(default, ActivityTraceFlags.Recorded)
            from traceState in Gen.OneOf(
                Gen.Constant((string?)null),
                ValidTraceStateArbitrary().Generator.Select(static value => (string?)value))
            select new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                traceFlags,
                traceState);

        return gen.ToArbitrary();
    }

    public static Arbitrary<Dictionary<string, string>> SafeBaggageDictionaryArbitrary()
    {
        var pairGen =
            from key in CreateString(BaggageChar, 1, 12)
            from value in CreateString(BaggageChar, 1, 24)
            select new KeyValuePair<string, string>(key, value);

        var gen = Gen.Sized(size =>
        {
            var maxCount = Math.Min(Math.Max(size + 1, 1), 20);

            return
                from count in Gen.Choose(0, maxCount)
                from pairs in Gen.ArrayOf(pairGen, count)
                select ToDictionary(pairs);
        });

        return gen.ToArbitrary();
    }

    public static Arbitrary<Dictionary<string, string>> BaggageDictionaryArbitrary()
    {
        var pairGen =
            from key in CreateString(BaggageChar, 0, 12)
            from value in CreateString(BaggageChar, 0, 32)
            select new KeyValuePair<string, string>(key, value);

        var gen = Gen.Sized(size =>
        {
            var maxCount = Math.Min(Math.Max(size + 1, 1), 256);

            return
                from count in Gen.Choose(0, maxCount)
                from pairs in Gen.ArrayOf(pairGen, count)
                select ToDictionary(pairs);
        });

        return gen.ToArbitrary();
    }

    public static Arbitrary<Dictionary<string, string[]>> TraceContextCarrierArbitrary()
    {
        var gen =
            from includeTraceParent in Gen.Elements(true, false)
            from includeTraceState in Gen.Elements(true, false)
            from traceParentValues in HeaderValuesArbitrary(4, 96).Generator
            from traceStateValues in HeaderValuesArbitrary(4, 256).Generator
            select CreateCarrier(
                ("traceparent", includeTraceParent, traceParentValues),
                ("tracestate", includeTraceState, traceStateValues));

        return gen.ToArbitrary();
    }

    public static Arbitrary<Dictionary<string, string[]>> BaggageCarrierArbitrary()
    {
        var gen =
            from includeBaggage in Gen.Elements(true, false)
            from baggageValues in HeaderValuesArbitrary(6, 512).Generator
            select CreateCarrier(("baggage", includeBaggage, baggageValues));

        return gen.ToArbitrary();
    }

    public static Arbitrary<string[]> OversizedBaggageValuesArbitrary()
    {
        var valueGen = CreateString(CompactBaggageValueChar, 1, 64);
        var gen = Gen.Sized(size =>
        {
            var maxCount = Math.Min(Math.Max((size * 4) + 181, 181), 512);

            return
                from count in Gen.Choose(181, maxCount)
                from values in Gen.ArrayOf(valueGen, count)
                select values;
        });

        return gen.ToArbitrary();
    }

    public static Arbitrary<Dictionary<string, string[]>> B3MultipleHeaderCarrierArbitrary()
    {
        var gen =
            from includeTraceId in Gen.Elements(true, false)
            from includeSpanId in Gen.Elements(true, false)
            from includeSampled in Gen.Elements(true, false)
            from includeFlags in Gen.Elements(true, false)
            from traceIdValues in HeaderValuesArbitrary(4, 64).Generator
            from spanIdValues in HeaderValuesArbitrary(4, 32).Generator
            from sampledValues in HeaderValuesArbitrary(4, 8).Generator
            from flagsValues in HeaderValuesArbitrary(4, 8).Generator
            select CreateCarrier(
                ("X-B3-TraceId", includeTraceId, traceIdValues),
                ("X-B3-SpanId", includeSpanId, spanIdValues),
                ("X-B3-Sampled", includeSampled, sampledValues),
                ("X-B3-Flags", includeFlags, flagsValues));

        return gen.ToArbitrary();
    }

    public static Arbitrary<Dictionary<string, string[]>> B3SingleHeaderCarrierArbitrary()
    {
        var gen =
            from includeCombined in Gen.Elements(true, false)
            from combinedValues in HeaderValuesArbitrary(4, 128).Generator
            select CreateCarrier(("b3", includeCombined, combinedValues));

        return gen.ToArbitrary();
    }

    public static Arbitrary<string> DelimiterFloodArbitrary(char delimiter, int minLength = 1024, int maxLength = 50_000)
    {
        var gen =
            from length in Gen.Choose(minLength, maxLength)
            select new string(delimiter, length);

        return gen.ToArbitrary();
    }

    public static Arbitrary<string> ValidTraceStateArbitrary()
    {
        var memberGen =
            from key in ValidTraceStateKey()
            from value in CreateString(TraceStateValueChar, 1, 24)
            select (key, value);

        var gen = Gen.Sized(size =>
        {
            var maxCount = Math.Min(Math.Max(size + 1, 1), 16);

            return
                from count in Gen.Choose(1, maxCount)
                from members in Gen.ArrayOf(memberGen, count)
                select string.Join(
                    ",",
                    members.Select(static (member, index) => $"{member.key}{index}={member.value}"));
        });

        return gen.ToArbitrary();
    }

    private static Arbitrary<string[]> HeaderValuesArbitrary(int maxCount, int maxLength)
    {
        var valueGen = CreateString(HeaderValueChar, 0, maxLength);
        var gen = Gen.Sized(size =>
        {
            var count = Math.Min(Math.Max(size + 1, 1), maxCount);

            return
                from actualCount in Gen.Choose(0, count)
                from values in Gen.ArrayOf(valueGen, actualCount)
                select values;
        });

        return gen.ToArbitrary();
    }

    private static Gen<string> CreateString(Gen<char> charGen, int minLength, int maxLength) =>
        from length in Gen.Choose(minLength, maxLength)
        from chars in Gen.ArrayOf(charGen, length)
        select new string(chars);

    private static Gen<string> ValidTraceStateKey()
    {
        var simpleKey =
            from length in Gen.Choose(1, 12)
            from first in LowerAlphaNumericChar
            from rest in Gen.ArrayOf(TraceStateKeyChar, length - 1)
            select $"{first}{new string(rest)}";

        var vendorKey =
            from tenantLength in Gen.Choose(1, 8)
            from vendorLength in Gen.Choose(1, 6)
            from tenantFirst in LowerAlphaNumericChar
            from tenantRest in Gen.ArrayOf(TraceStateKeyChar, tenantLength - 1)
            from vendorFirst in LowerAlphaNumericChar
            from vendorRest in Gen.ArrayOf(TraceStateKeyChar, vendorLength - 1)
            select $"{tenantFirst}{new string(tenantRest)}@{vendorFirst}{new string(vendorRest)}";

        return Gen.OneOf(simpleKey, vendorKey);
    }

    private static Dictionary<string, string> ToDictionary(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in pairs)
        {
            dictionary[pair.Key] = pair.Value;
        }

        return dictionary;
    }

    private static Dictionary<string, string[]> CreateCarrier(params (string Key, bool Include, string[] Values)[] entries)
    {
        var carrier = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, include, values) in entries)
        {
            if (include)
            {
                carrier[key] = [.. values];
            }
        }

        return carrier;
    }
}
