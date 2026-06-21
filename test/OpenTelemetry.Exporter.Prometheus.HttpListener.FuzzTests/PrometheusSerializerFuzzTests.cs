// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Exporter.Prometheus.Serialization;

namespace OpenTelemetry.Exporter.Prometheus.FuzzTests;

public class PrometheusSerializerFuzzTests
{
    private const int MaxTests = 200;

    [Property(MaxTest = MaxTests)]
    public Property WriteAsciiStringNoEscapeMatchesReferenceImplementation() => Prop.ForAll(
        Generators.AsciiStringArbitrary(),
        static (value) => Serialize(value, TextFormatSerializer.WriteAsciiStringNoEscape).SequenceEqual(ReferenceWriteAsciiStringNoEscape(value)));

    [Property(MaxTest = MaxTests)]
    public Property WritePrometheusLabelKeyMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => Serialize(value, static (buffer, cursor, text) => TextFormatSerializer.WriteLabelKey(buffer, cursor, text)).SequenceEqual(ReferenceWriteLabelKey(value)));

    [Property(MaxTest = MaxTests)]
    public Property WriteOpenMetricsLabelKeyMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => SerializeOpenMetricsLabelKey(value).SequenceEqual(ReferenceWriteLabelKey(value)));

    [Property(MaxTest = MaxTests)]
    public Property WriteLabelValueMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => Serialize(value, TextFormatSerializer.WriteLabelValue).SequenceEqual(ReferenceWriteLabelValue(value)));

    [Property(MaxTest = MaxTests)]
    public Property WriteUnicodeStringMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => Serialize(value, TextFormatSerializer.WriteUnicodeString).SequenceEqual(ReferenceWriteUnicodeString(value)));

    [Property(MaxTest = MaxTests)]
    public Property WriteLongMatchesReferenceImplementation() => Prop.ForAll(
        Generators.LongArbitrary(),
        static (value) => SerializeLong(value).SequenceEqual(ReferenceWriteLong(value)));

    [Property(MaxTest = MaxTests)]
    public Property WriteDoubleMatchesReferenceImplementation() => Prop.ForAll(
        Generators.DoubleArbitrary(),
        static (value) => SerializeDouble(value).SequenceEqual(ReferenceWriteDouble(value)));

    [Property(MaxTest = MaxTests)]
    public Property EscapeNameWithDotsMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => PrometheusEscaping.EscapeName(value, EscapingScheme.Dots) == ReferenceEscapeName(value, EscapingScheme.Dots));

    [Property(MaxTest = MaxTests)]
    public Property EscapeNameWithValuesMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => PrometheusEscaping.EscapeName(value, EscapingScheme.Values) == ReferenceEscapeName(value, EscapingScheme.Values));

    [Property(MaxTest = MaxTests)]
    public Property EscapeNameToBufferMatchesStringOverload() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) =>
            SerializeEscapeName(value, EscapingScheme.Dots).SequenceEqual(Encoding.ASCII.GetBytes(PrometheusEscaping.EscapeName(value, EscapingScheme.Dots))) &&
            SerializeEscapeName(value, EscapingScheme.Values).SequenceEqual(Encoding.ASCII.GetBytes(PrometheusEscaping.EscapeName(value, EscapingScheme.Values))));

    [Property(MaxTest = MaxTests)]
    public Property IsValidLegacyNameMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => PrometheusEscaping.IsValidLegacyName(value) == ReferenceIsValidLegacyName(value, allowColon: true));

    [Property(MaxTest = MaxTests)]
    public Property IsValidLegacyLabelNameMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => PrometheusEscaping.IsValidLegacyLabelName(value) == ReferenceIsValidLegacyName(value, allowColon: false));

    [Property(MaxTest = MaxTests)]
    public Property WriteLabelNameMatchesReferenceImplementation() => Prop.ForAll(
        Generators.PrometheusStringArbitrary(),
        static (value) => Serialize(value, static (buffer, cursor, text) => TextFormatSerializer.WriteLabelName(buffer, cursor, text)).SequenceEqual(ReferenceWriteLabelName(value)));

    private static byte[] Serialize(string value, Func<byte[], int, string, int> writer)
    {
        var buffer = new byte[(value.Length * 8) + 16];
        var cursor = writer(buffer, 0, value);
        return buffer.AsSpan(0, cursor).ToArray();
    }

    private static byte[] SerializeOpenMetricsLabelKey(string value)
    {
        var buffer = new byte[(value.Length * 8) + 16];
        var cursor = TextFormatSerializer.WriteLabelKey(buffer, 0, value);
        return buffer.AsSpan(0, cursor).ToArray();
    }

    private static byte[] SerializeEscapeName(string value, EscapingScheme scheme)
    {
        var buffer = new byte[(value.Length * 8) + 16];
        var cursor = PrometheusEscaping.EscapeName(buffer, 0, value, scheme);
        return buffer.AsSpan(0, cursor).ToArray();
    }

    private static byte[] SerializeLong(long value)
    {
        var buffer = new byte[64];
        var cursor = TextFormatSerializer.WriteLong(buffer, 0, value);
        return buffer.AsSpan(0, cursor).ToArray();
    }

    private static byte[] SerializeDouble(double value)
    {
        var buffer = new byte[64];
        var cursor = TextFormatSerializer.WriteDouble(buffer, 0, value);
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

        var lastCharUnderscore = false;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var isAllowed =
                c is (>= 'A' and <= 'Z') or
                (>= 'a' and <= 'z') or
                (>= '0' and <= '9') or
                '_';

            if (i == 0 && c is >= '0' and <= '9')
            {
                bytes.Add((byte)'_');
                lastCharUnderscore = true;
            }

            if (!isAllowed)
            {
                if (!lastCharUnderscore)
                {
                    bytes.Add((byte)'_');
                    lastCharUnderscore = true;
                }

                continue;
            }

            bytes.Add((byte)c);
            lastCharUnderscore = c == '_';
        }

        return [.. bytes];
    }

    private static byte[] ReferenceWriteLabelValue(string value) => ReferenceWriteEscapedString(value, escapeQuotationMarks: true);

    private static byte[] ReferenceWriteUnicodeString(string value) => ReferenceWriteEscapedString(value, escapeQuotationMarks: false);

    private static byte[] ReferenceWriteLong(long value) => Encoding.UTF8.GetBytes(value.ToString(CultureInfo.InvariantCulture));

    private static byte[] ReferenceWriteDouble(double value) => value switch
    {
        var doubleValue when double.IsPositiveInfinity(doubleValue) => Encoding.UTF8.GetBytes("+Inf"),
        var doubleValue when double.IsNegativeInfinity(doubleValue) => Encoding.UTF8.GetBytes("-Inf"),
        var doubleValue when double.IsNaN(doubleValue) => Encoding.UTF8.GetBytes("NaN"),
        _ => Encoding.UTF8.GetBytes(value.ToString("G17", CultureInfo.InvariantCulture)),
    };

    // An independent re-implementation of the dots and values escaping schemes used to validate
    // PrometheusEscaping.EscapeName. It deliberately uses different mechanisms (manual surrogate
    // decoding, a StringBuilder, and int.ToString("x")) than the production code.
    private static string ReferenceEscapeName(string value, EscapingScheme scheme)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (scheme == EscapingScheme.Values && ReferenceIsValidLegacyName(value, allowColon: true))
        {
            return value;
        }

        var text = new StringBuilder(value.Length + 8);

        if (scheme == EscapingScheme.Values)
        {
            text.Append("U__");
        }

        var index = 0;

        while (index < value.Length)
        {
            var codePoint = ReferenceGetCodePoint(value, index, out var charsConsumed, out var isValidRune);

            if (codePoint == '_')
            {
                text.Append("__");
            }
            else if (scheme == EscapingScheme.Dots && codePoint == '.')
            {
                text.Append("_dot_");
            }
            else if (ReferenceIsValidLegacyRune(codePoint, index == 0))
            {
                text.Append((char)codePoint);
            }
            else if (scheme == EscapingScheme.Dots)
            {
                text.Append("__");
            }
            else if (!isValidRune)
            {
                text.Append("_FFFD_");
            }
            else
            {
                text.Append('_').Append(codePoint.ToString("x", CultureInfo.InvariantCulture)).Append('_');
            }

            index += charsConsumed;
        }

        return text.ToString();
    }

    private static bool ReferenceIsValidLegacyName(string value, bool allowColon)
    {
        if (value.Length == 0)
        {
            return false;
        }

        var index = 0;

        while (index < value.Length)
        {
            var codePoint = ReferenceGetCodePoint(value, index, out var charsConsumed, out _);

            if (!ReferenceIsValidLegacyRune(codePoint, index == 0) || (codePoint == ':' && !allowColon))
            {
                return false;
            }

            index += charsConsumed;
        }

        return true;
    }

    // An independent re-implementation of TextFormatSerializer.WriteLabelName: a non-legacy label
    // name is emitted as a double-quoted UTF-8 string (escaping '"', '\' and '\n'); a legacy name is
    // written verbatim as ASCII bytes.
    private static byte[] ReferenceWriteLabelName(string value)
    {
        if (ReferenceIsValidLegacyName(value, allowColon: false))
        {
            return ReferenceWriteAsciiStringNoEscape(value);
        }

        var escaped = ReferenceWriteEscapedString(value, escapeQuotationMarks: true);

        return [(byte)'"', .. escaped, (byte)'"'];
    }

    private static bool ReferenceIsValidLegacyRune(int codePoint, bool isFirst) =>
        codePoint is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or ':' ||
        (!isFirst && codePoint is >= '0' and <= '9');

    private static int ReferenceGetCodePoint(string value, int index, out int charsConsumed, out bool isValidRune)
    {
        var character = value[index];

        if (char.IsHighSurrogate(character) &&
            index + 1 < value.Length &&
            char.IsLowSurrogate(value[index + 1]))
        {
            charsConsumed = 2;
            isValidRune = true;
            return char.ConvertToUtf32(character, value[index + 1]);
        }

        if (char.IsSurrogate(character))
        {
            charsConsumed = 1;
            isValidRune = false;
            return 0xFFFD;
        }

        charsConsumed = 1;
        isValidRune = true;
        return character;
    }

    private static byte[] ReferenceWriteEscapedString(string value, bool escapeQuotationMarks)
    {
        var text = new StringBuilder(value.Length);

        foreach (var c in value)
        {
            switch (c)
            {
                case '"' when escapeQuotationMarks:
                    text.Append("\\\"");
                    break;
                case '\\':
                    text.Append("\\\\");
                    break;
                case '\n':
                    text.Append("\\n");
                    break;
                default:
                    text.Append(c);
                    break;
            }
        }

        return Encoding.UTF8.GetBytes(text.ToString());
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

        public static Arbitrary<double> DoubleArbitrary()
        {
            var generator =
                from mantissa in Gen.Choose(-1_000_000, 1_000_000)
                from exponent in Gen.Choose(-12, 12)
                select mantissa * Math.Pow(10, exponent);

            var finite = Gen.OneOf(
                Gen.Elements(-1d, 0d, 1d, double.Epsilon, double.MinValue, double.MaxValue),
                generator);

            return Gen.OneOf(
                finite,
                Gen.Constant(double.PositiveInfinity),
                Gen.Constant(double.NegativeInfinity),
                Gen.Constant(double.NaN)).ToArbitrary();
        }

        public static Arbitrary<long> LongArbitrary()
        {
            var generator = from high in Gen.Choose(int.MinValue, int.MaxValue)
                            from low in Gen.Choose(int.MinValue, int.MaxValue)
                            select ((long)high << 32) | (uint)low;

            return Gen.OneOf(Gen.Elements(long.MinValue, -1L, 0L, 1L, long.MaxValue), generator).ToArbitrary();
        }

        public static Arbitrary<object?> LabelValueArbitrary()
        {
            var chars = Gen.Choose(0, 0xFFFF).Select(static value => (object?)(char)value);
            var decimals = Gen.Choose(int.MinValue, int.MaxValue).Select(static value => (object?)(value / 10m));
            var unsignedLongs =
                from high in Gen.Choose(0, int.MaxValue)
                from low in Gen.Choose(int.MinValue, int.MaxValue)
                select (object?)(((ulong)(uint)high << 32) | (uint)low);

            return Gen.OneOf(
                Gen.Constant((object?)null),
                PrometheusStringArbitrary().Generator.Select(static value => (object?)value),
                Gen.Elements<object?>(true, false),
                LongArbitrary().Generator.Select(static value => (object?)value),
                unsignedLongs,
                DoubleArbitrary().Generator.Select(static value => (object?)value),
                decimals,
                chars).ToArbitrary();
        }

        private static Gen<string> CreateString(Gen<char> charGen, int maxLength) =>
            Gen.Sized(size =>
                from length in Gen.Choose(0, Math.Min((size * 2) + 1, maxLength))
                from chars in Gen.ArrayOf(charGen, length)
                select new string(chars));
    }
}
