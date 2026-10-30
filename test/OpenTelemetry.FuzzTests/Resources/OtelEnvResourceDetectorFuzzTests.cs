// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Resources.FuzzTests;

/// <summary>
/// Fuzz tests for <see cref="OtelEnvResourceDetector"/>, which parses <c>OTEL_RESOURCE_ATTRIBUTES</c>.
/// </summary>
/// <remarks>
/// The specification defines the value as a list of <c>key=value</c> pairs in the W3C Baggage format,
/// separated by commas, where values are percent-encoded (and only percent-encoded).
/// </remarks>
public class OtelEnvResourceDetectorFuzzTests
{
    private const int MaxTests = 300;

    [Property(MaxTest = MaxTests)]
    public Property ArbitraryValueNeverThrows() => Prop.ForAll(
        Generators.ArbitraryValueArbitrary(),
        static (value) =>
        {
            _ = Detect(value);

            return true;
        });

    [Property(MaxTest = MaxTests)]
    public Property PercentEncodedAttributesRoundTrip() => Prop.ForAll(
        Generators.AttributesArbitrary(),
        static (attributes) =>
        {
            var value = string.Join(",", attributes.Select(static a => a.Key + "=" + Uri.EscapeDataString(a.Value)));

            return Matches(Detect(value), attributes);
        });

    [Property(MaxTest = MaxTests)]
    public Property UnencodedBaggageCharactersAreNotTransformed() => Prop.ForAll(
        Generators.RawBaggageAttributesArbitrary(),
        static (attributes) =>
        {
            // Characters such as '+' are valid, unencoded, baggage-octets. Only percent-escapes may be decoded.
            var value = string.Join(",", attributes.Select(static a => a.Key + "=" + a.Value));

            return Matches(Detect(value), attributes);
        });

    [Property(MaxTest = MaxTests)]
    public Property PercentEncodedWhitespaceInValuesIsPreserved() => Prop.ForAll(
        Generators.AttributesArbitrary(),
        static (attributes) =>
        {
            var padded = attributes.Select(static a => new KeyValuePair<string, string>(a.Key, " " + a.Value + " ")).ToList();
            var value = string.Join(",", padded.Select(static a => a.Key + "=" + Uri.EscapeDataString(a.Value)));

            return Matches(Detect(value), padded);
        });

    [Property(MaxTest = MaxTests)]
    public Property SurroundingWhitespaceAroundPairsIsIgnored() => Prop.ForAll(
        Generators.RawBaggageAttributesArbitrary(),
        static (attributes) =>
        {
            var value = string.Join(" , ", attributes.Select(static a => " " + a.Key + " = " + Uri.EscapeDataString(a.Value) + " "));

            return Matches(Detect(value), attributes);
        });

    private static Resource Detect(string value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvResourceDetector.EnvVarKey] = value })
            .Build();

        return new OtelEnvResourceDetector(configuration).Detect();
    }

    private static bool Matches(Resource resource, List<KeyValuePair<string, string>> expected)
    {
        var actual = resource.Attributes.ToDictionary(static a => a.Key, static a => a.Value);

        return actual.Count == expected.Count &&
               expected.All(e => actual.TryGetValue(e.Key, out var value) && Equals(value, e.Value));
    }

    private static class Generators
    {
        private static readonly Gen<char> KeyChar = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.".ToCharArray());

        private static readonly Gen<char> UnreservedChar = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~".ToCharArray());

        // The remaining baggage-octets, which are legal in a value without any encoding.
        private static readonly Gen<char> RawBaggageChar = Gen.Frequency(
            (6, UnreservedChar),
            (2, Gen.Elements("+*!$&'()/:?@[]{}|^<>#".ToCharArray())));

        private static readonly Gen<char> AnyValueChar = Gen.Frequency(
            (4, UnreservedChar),
            (3, Gen.Elements(" =+%&;:/?#[]@!$'()*,<>{}|^`".ToCharArray())),
            (2, Gen.Choose(0xA0, 0xD7FF).Select(static c => (char)c)),
            (1, Gen.Choose(0xE000, 0xFFFD).Select(static c => (char)c)));

        public static Arbitrary<string> ArbitraryValueArbitrary()
        {
            var charGen = Gen.Frequency(
                (6, Gen.Elements("abcXYZ019,=%-_ +".ToCharArray())),
                (2, Gen.Elements("%2C%3D%20%25%00%FF%C3%A9%ZZ".ToCharArray())),
                (1, Gen.Choose(0, 0xFFFF).Select(static c => (char)c)));

            return Gen.Sized(size =>
                from length in Gen.Choose(0, Math.Min((size * 3) + 1, 200))
                from chars in Gen.ArrayOf(charGen, length)
                select new string(chars)).ToArbitrary();
        }

        public static Arbitrary<List<KeyValuePair<string, string>>> AttributesArbitrary() => AttributesGen(AnyValueChar).ToArbitrary();

        public static Arbitrary<List<KeyValuePair<string, string>>> RawBaggageAttributesArbitrary() => AttributesGen(RawBaggageChar).ToArbitrary();

        private static Gen<List<KeyValuePair<string, string>>> AttributesGen(Gen<char> valueChar) =>
            from count in Gen.Choose(1, 5)
            from keys in Gen.ArrayOf(KeyGen(), count)
            from values in Gen.ArrayOf(ValueGen(valueChar), count)
            select keys
                .Select(static (key, index) => (key, index))
                .GroupBy(static x => x.key, StringComparer.Ordinal)
                .Select(g => new KeyValuePair<string, string>(g.Key, values[g.First().index]))
                .ToList();

        private static Gen<string> KeyGen() =>
            from length in Gen.Choose(1, 10)
            from chars in Gen.ArrayOf(KeyChar, length)
            select new string(chars);

        private static Gen<string> ValueGen(Gen<char> valueChar) =>
            from length in Gen.Choose(0, 16)
            from chars in Gen.ArrayOf(valueChar, length)
            select new string(chars);
    }
}
