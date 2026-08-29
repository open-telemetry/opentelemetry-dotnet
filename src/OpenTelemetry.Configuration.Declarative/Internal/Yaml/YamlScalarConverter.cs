// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Converts a <see cref="ResolvedYamlScalar"/> produced by <see cref="YamlScalarResolver"/>
/// to a <see cref="ConfigValue"/>.
/// </summary>
internal static class YamlScalarConverter
{
    /// <summary>
    /// Converts a resolved YAML scalar to a <see cref="ConfigValue"/>.
    /// </summary>
    /// <remarks>
    /// This method is total for scalars produced by <see cref="YamlScalarResolver"/>: it never
    /// throws and has no failure outcome. An integer that exceeds the <see cref="long"/> range is
    /// returned as <see cref="ConfigValue.UnrepresentableInteger()"/> rather than causing an error.
    /// <para/>
    /// Hand-built <see cref="ResolvedYamlScalar"/> values whose kind does not match the text
    /// (for example an unknown kind, a non-boolean under <see cref="YamlScalarKind.Boolean"/>, or a
    /// character that is not valid for hex/octal integer text) may throw
    /// <see cref="InvalidOperationException"/>. That is a programming error, not a conversion failure.
    /// </remarks>
    /// <param name="scalar">The resolved scalar to convert.</param>
    /// <returns>A <see cref="ConfigValue"/> representing the scalar.</returns>
    internal static ConfigValue Convert(ResolvedYamlScalar scalar) =>
        scalar.Kind switch
        {
            YamlScalarKind.Boolean => ConvertBoolean(scalar.Value),
            YamlScalarKind.Float => ConvertFloat(scalar.Value),
            YamlScalarKind.Integer => ConvertInteger(scalar.Value),
            YamlScalarKind.Null => ConfigValue.Null,
            YamlScalarKind.String => ConfigValue.String(scalar.Value),
            _ => throw new InvalidOperationException($"Unhandled {nameof(YamlScalarKind)}: {scalar.Kind}."),
        };

    private static ConfigValue ConvertBoolean(string value) =>
        YamlScalarResolver.TryGetBoolean(value, out var boolValue)
            ? ConfigValue.Boolean(boolValue)
            : throw new InvalidOperationException(
                $"Boolean kind requires a YAML 1.2 boolean value, got '{value}'.");

    private static ConfigValue ConvertInteger(string value) =>
        value switch
        {
            { Length: >= 3 } when value[0] == '0' && value[1] == 'x' =>
                AccumulateUnsigned(value, start: 2, numberBase: 16),
            { Length: >= 3 } when value[0] == '0' && value[1] == 'o' =>
                AccumulateUnsigned(value, start: 2, numberBase: 8),

            // A false return can only mean out of range; the resolver guarantees the decimal form is valid.
            _ when long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) =>
                ConfigValue.Integer(l),
            _ => ConfigValue.UnrepresentableInteger(),
        };

    // !!float accepts integer notation, so hex and octal values can arrive with Float kind.
    // Accumulate directly into double so values beyond the long range saturate to +Infinity
    // naturally rather than becoming unrepresentable integers.
    private static ConfigValue ConvertFloat(string value) =>
        value switch
        {
            _ when YamlScalarResolver.IsInfinity(value) =>
                ConfigValue.Double(IsNegative(value) ? double.NegativeInfinity : double.PositiveInfinity),
            _ when YamlScalarResolver.IsNaN(value) => ConfigValue.Double(double.NaN),
            { Length: >= 3 } when value[0] == '0' && value[1] == 'x' =>
                ConfigValue.Double(AccumulateDouble(value, start: 2, numberBase: 16)),
            { Length: >= 3 } when value[0] == '0' && value[1] == 'o' =>
                ConfigValue.Double(AccumulateDouble(value, start: 2, numberBase: 8)),
            _ => ConvertDecimalFloat(value),
        };

    // Before .NET Core 3.0, double.TryParse fails instead of saturating to +/-Infinity on overflow,
    // and does not preserve negative zero. The extra arms keep behavior consistent across TFMs.
    private static ConfigValue ConvertDecimalFloat(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) switch
        {
#if !NET
            true when d == 0.0 && IsNegative(value) && BitConverter.DoubleToInt64Bits(d) >= 0 =>
                ConfigValue.Double(-d),
#endif
            true => ConfigValue.Double(d),

            // Overflow on those frameworks, or text that is not a float at all.
            false when IsNegative(value) => ConfigValue.Double(double.NegativeInfinity),
            false => ConfigValue.Double(double.PositiveInfinity),
        };

    private static bool IsNegative(string value) =>
        value is { Length: > 0 } && value[0] == '-';

    private static ConfigValue AccumulateUnsigned(string value, int start, ulong numberBase)
    {
        var accumulator = 0UL;
        for (var i = start; i < value.Length; i++)
        {
            var digit = DigitValue(value[i], numberBase);
            if (accumulator > (ulong.MaxValue - digit) / numberBase)
            {
                return ConfigValue.UnrepresentableInteger();
            }

            accumulator = (accumulator * numberBase) + digit;
        }

        return accumulator > long.MaxValue
            ? ConfigValue.UnrepresentableInteger()
            : ConfigValue.Integer((long)accumulator);
    }

    private static double AccumulateDouble(string value, int start, ulong numberBase)
    {
        var accumulator = 0.0;
        for (var i = start; i < value.Length; i++)
        {
            accumulator = (accumulator * numberBase) + DigitValue(value[i], numberBase);
        }

        return accumulator;
    }

    private static ulong DigitValue(char c, ulong numberBase)
    {
        var digit = (ulong)(c switch
        {
            _ when char.IsAsciiDigit(c) => c - '0',
            _ when char.IsAsciiLetterLower(c) && c <= 'f' => c - 'a' + 10,
            _ when char.IsAsciiLetterUpper(c) && c <= 'F' => c - 'A' + 10,
            _ => throw new InvalidOperationException($"Not a valid digit: '{c}'."),
        });

        if (digit >= numberBase)
        {
            var form = numberBase == 16 ? "hexadecimal" : "octal";
            throw new InvalidOperationException($"Digit '{c}' is not valid in {form} integer text.");
        }

        return digit;
    }
}
