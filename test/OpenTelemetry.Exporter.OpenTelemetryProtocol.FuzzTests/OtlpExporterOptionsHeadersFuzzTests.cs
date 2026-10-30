// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.FuzzTests;

/// <summary>
/// Fuzz tests for the parsing of the <c>OTEL_EXPORTER_OTLP_HEADERS</c> value
/// (<see cref="OtlpExporterOptionsExtensions.GetHeaders{THeaders}"/>).
/// </summary>
/// <remarks>
/// The specification defines the value as a list of <c>key=value</c> pairs in the W3C Baggage format,
/// separated by commas, where each value is percent-encoded.
/// </remarks>
public class OtlpExporterOptionsHeadersFuzzTests
{
    private const int MaxTests = 300;

    [Property(MaxTest = MaxTests)]
    public Property ArbitraryHeaderStringsOnlyFailWithArgumentException() => Prop.ForAll(
        Generators.ArbitraryHeaderArbitrary(),
        static (headers) =>
        {
            try
            {
                var options = new OtlpExporterOptions { Headers = headers };

                _ = options.GetHeaders<List<KeyValuePair<string, string>>>(static (list, key, value) => list.Add(new(key, value)));

                return true;
            }
            catch (ArgumentException)
            {
                return true;
            }
        });

    [Property(MaxTest = MaxTests)]
    public Property PlainHeadersRoundTrip() => Prop.ForAll(
        Generators.PlainHeadersArbitrary(),
        static (pairs) => RoundTrips(pairs, EncodeAll));

    [Property(MaxTest = MaxTests)]
    public Property PercentEncodedValuesRoundTrip() => Prop.ForAll(
        Generators.PercentEncodableHeadersArbitrary(),
        static (pairs) => RoundTrips(pairs, EncodeAll));

    [Property(MaxTest = MaxTests)]
    public Property PercentEncodedCommasInValuesDoNotSplitHeaders() => Prop.ForAll(
        Generators.PlainHeadersArbitrary(),
        Generators.ValueArbitrary(),
        static (pairs, value) =>
        {
            // A comma that has been percent-encoded is data, not a separator.
            var withComma = pairs.Select(static p => new KeyValuePair<string, string>(p.Key, p.Value + ",x")).ToList();
            withComma.Add(new("last", value + ",y"));

            return RoundTrips(withComma, EncodeAll);
        });

    [Property(MaxTest = MaxTests)]
    public Property PercentEncodedLeadingAndTrailingWhitespaceInValuesIsPreserved() => Prop.ForAll(
        Generators.PlainHeadersArbitrary(),
        static (pairs) =>
        {
            var padded = pairs.Select(static p => new KeyValuePair<string, string>(p.Key, " " + p.Value + " ")).ToList();

            return RoundTrips(padded, EncodeAll);
        });

    [Property(MaxTest = MaxTests)]
    public Property EachSeparatedPairIsPassedToTheCallback() => Prop.ForAll(
        Generators.PlainHeadersArbitrary(),
        static (pairs) =>
        {
            var header = string.Join(",", pairs.Select(static p => p.Key + "=" + p.Value));
            var options = new OtlpExporterOptions { Headers = header };

            var actual = options.GetHeaders<List<KeyValuePair<string, string>>>(static (list, key, value) => list.Add(new(key, value)));

            return actual.Count == pairs.Count + options.StandardHeaders.Length;
        });

    private static string EncodeAll(List<KeyValuePair<string, string>> pairs)
        => string.Join(",", pairs.Select(static p => p.Key + "=" + Uri.EscapeDataString(p.Value)));

    private static bool RoundTrips(
        List<KeyValuePair<string, string>> pairs,
        Func<List<KeyValuePair<string, string>>, string> encode)
    {
        var options = new OtlpExporterOptions { Headers = encode(pairs) };

        var actual = options.GetHeaders<List<KeyValuePair<string, string>>>(static (list, key, value) => list.Add(new(key, value)));

        // Standard headers (e.g. User-Agent) are appended after the user's headers.
        var user = actual.Take(actual.Count - options.StandardHeaders.Length).ToList();

        return user.Count == pairs.Count && user.Zip(pairs, static (a, e) => a.Key == e.Key && a.Value == e.Value).All(static x => x);
    }

    private static class Generators
    {
        private static readonly Gen<char> KeyChar = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.".ToCharArray());

        private static readonly Gen<char> PlainValueChar = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~".ToCharArray());

        // Characters which must be percent-encoded to appear in a header list, plus non-ASCII text.
        private static readonly Gen<char> SpecialValueChar = Gen.Frequency(
            (4, PlainValueChar),
            (3, Gen.Elements(" =+%&;:/?#[]@!$'()*\"<>{}|^`".ToCharArray())),
            (2, Gen.Choose(0xA0, 0xD7FF).Select(static c => (char)c)),
            (1, Gen.Choose(0xE000, 0xFFFD).Select(static c => (char)c)));

        public static Arbitrary<string?> ArbitraryHeaderArbitrary()
        {
            var charGen = Gen.Frequency(
                (6, Gen.Elements("abcXYZ019,=%-_ ".ToCharArray())),
                (2, Gen.Elements("%2C%3D%20%25%00%FF%C3%A9%ZZ".ToCharArray())),
                (1, Gen.Choose(0, 0xFFFF).Select(static c => (char)c)));

            var text = Gen.Sized(size =>
                from length in Gen.Choose(0, Math.Min((size * 3) + 1, 200))
                from chars in Gen.ArrayOf(charGen, length)
                select (string?)new string(chars));

            return Gen.OneOf(Gen.Constant((string?)null), text).ToArbitrary();
        }

        public static Arbitrary<string> ValueArbitrary() => Gen.Sized(size =>
            from length in Gen.Choose(0, Math.Min(size + 1, 20))
            from chars in Gen.ArrayOf(SpecialValueChar, length)
            select new string(chars)).ToArbitrary();

        public static Arbitrary<List<KeyValuePair<string, string>>> PlainHeadersArbitrary()
            => HeadersGen(PlainValueChar).ToArbitrary();

        public static Arbitrary<List<KeyValuePair<string, string>>> PercentEncodableHeadersArbitrary()
            => HeadersGen(SpecialValueChar).ToArbitrary();

        private static Gen<List<KeyValuePair<string, string>>> HeadersGen(Gen<char> valueChar) =>
            from count in Gen.Choose(1, 5)
            from pairs in Gen.ArrayOf(PairGen(valueChar), count)
            select pairs.ToList();

        private static Gen<KeyValuePair<string, string>> PairGen(Gen<char> valueChar) =>
            from keyLength in Gen.Choose(1, 10)
            from key in Gen.ArrayOf(KeyChar, keyLength)
            from valueLength in Gen.Choose(0, 16)
            from value in Gen.ArrayOf(valueChar, valueLength)
            select new KeyValuePair<string, string>(new string(key), new string(value));
    }
}
