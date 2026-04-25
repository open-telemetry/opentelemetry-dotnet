// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace OpenTelemetry.Exporter.Prometheus.FuzzTests;

public class PrometheusSerializerFuzzTests
{
    private const int MaxTests = 200;

    [Property(MaxTest = MaxTests)]
    public Property WriteAsciiStringNoEscapeMatchesReferenceImplementation() => Prop.ForAll(
        Generators.AsciiStringArbitrary(),
        static (value) => Serialize(value, PrometheusSerializer.WriteAsciiStringNoEscape).SequenceEqual(ReferenceWriteAsciiStringNoEscape(value)));

    [Property(MaxTest = MaxTests)]
    public Property WriteLabelKeyMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => Serialize(value, PrometheusSerializer.WriteLabelKey).SequenceEqual(ReferenceWriteLabelKey(value)));

    [Property(MaxTest = MaxTests)]
    public Property WriteLabelValueMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => Serialize(value, PrometheusSerializer.WriteLabelValue).SequenceEqual(ReferenceWriteLabelValue(value)));

    [Property(MaxTest = MaxTests)]
    public Property WriteUnicodeStringMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => Serialize(value, PrometheusSerializer.WriteUnicodeString).SequenceEqual(ReferenceWriteUnicodeString(value)));

    private static byte[] Serialize(string value, Func<byte[], int, string, int> writer)
    {
        var buffer = new byte[(value.Length * 8) + 16];
        var cursor = writer(buffer, 0, value);
        return buffer.AsSpan(0, cursor).ToArray();
    }

    private static byte[] ReferenceWriteAsciiStringNoEscape(string value)
    {
        var bytes = new byte[value.Length];
        for (var i = 0; i < value.Length; i++)
        {
            bytes[i] = unchecked((byte)value[i]);
        }

        return bytes;
    }

    private static byte[] ReferenceWriteLabelKey(string value)
    {
        var bytes = new List<byte>(value.Length + 1);
        if (string.IsNullOrEmpty(value))
        {
            bytes.Add((byte)'_');
            return [.. bytes];
        }

        if (value[0] is >= '0' and <= '9')
        {
            bytes.Add((byte)'_');
        }

        foreach (var c in value)
        {
            bytes.Add(c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') ? (byte)c : (byte)'_');
        }

        return [.. bytes];
    }

    private static byte[] ReferenceWriteLabelValue(string value) => ReferenceWriteEscapedString(value, escapeQuotationMarks: true);

    private static byte[] ReferenceWriteUnicodeString(string value) => ReferenceWriteEscapedString(value, escapeQuotationMarks: false);

    private static byte[] ReferenceWriteEscapedString(string value, bool escapeQuotationMarks)
    {
        var bytes = new List<byte>(value.Length * 3);

        foreach (var c in value)
        {
            switch ((ushort)c)
            {
                case '"' when escapeQuotationMarks:
                    bytes.Add((byte)'\\');
                    bytes.Add((byte)'"');
                    break;
                case '\\':
                    bytes.Add((byte)'\\');
                    bytes.Add((byte)'\\');
                    break;
                case '\n':
                    bytes.Add((byte)'\\');
                    bytes.Add((byte)'n');
                    break;
                default:
                    AppendUnicodeNoEscape(bytes, c);
                    break;
            }
        }

        return [.. bytes];
    }

    private static void AppendUnicodeNoEscape(List<byte> bytes, ushort ordinal)
    {
        if (ordinal <= 0x7F)
        {
            bytes.Add(unchecked((byte)ordinal));
        }
        else if (ordinal <= 0x07FF)
        {
            bytes.Add(unchecked((byte)(0b_1100_0000 | (ordinal >> 6))));
            bytes.Add(unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111))));
        }
        else
        {
            bytes.Add(unchecked((byte)(0b_1110_0000 | (ordinal >> 12))));
            bytes.Add(unchecked((byte)(0b_1000_0000 | ((ordinal >> 6) & 0b_0011_1111))));
            bytes.Add(unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111))));
        }
    }

    private static class Generators
    {
        public static Arbitrary<string> AsciiStringArbitrary()
        {
            var asciiChar = Gen.Choose(0, 0x7F).Select(static c => (char)c);
            return CreateString(asciiChar, maxLength: 256).ToArbitrary();
        }

        public static Arbitrary<string> PrometheusStringArbitrary()
        {
            var charGen = Gen.Choose(0, 0xFFFF).Select(static c => (char)c);
            return CreateString(charGen, maxLength: 128).ToArbitrary();
        }

        private static Gen<string> CreateString(Gen<char> charGen, int maxLength) =>
            Gen.Sized(size =>
                from length in Gen.Choose(0, Math.Min((size * 2) + 1, maxLength))
                from chars in Gen.ArrayOf(charGen, length)
                select new string(chars));
    }
}
