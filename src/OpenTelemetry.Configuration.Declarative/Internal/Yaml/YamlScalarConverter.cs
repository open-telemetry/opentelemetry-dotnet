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
            YamlScalarKind.Null => ConfigValue.Null,
            YamlScalarKind.String => ConfigValue.String(scalar.Value),
            YamlScalarKind.Boolean => ConvertBoolean(scalar.Value),
            YamlScalarKind.Integer => ConvertInteger(scalar.Value),
            YamlScalarKind.Float => ConvertFloat(scalar.Value),
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
                AccumulateUnsigned(value, start: 2, radix: 16),
            { Length: >= 3 } when value[0] == '0' && value[1] == 'o' =>
                AccumulateUnsigned(value, start: 2, radix: 8),

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
            ".nan" or ".NaN" or ".NAN" => ConfigValue.Double(double.NaN),
            { Length: >= 3 } when value[0] == '0' && value[1] == 'x' =>
                ConfigValue.Double(AccumulateDouble(value, start: 2, radix: 16)),
            { Length: >= 3 } when value[0] == '0' && value[1] == 'o' =>
                ConfigValue.Double(AccumulateDouble(value, start: 2, radix: 8)),
            _ => ConvertDecimalFloat(value),
        };

    // On .NET Framework, double.TryParse returns false for magnitude overflow (unlike .NET Core
    // which saturates to +/-Infinity), and does not preserve negative zero for "-0.0" or negative
    // underflow inputs. Apply the sign from the value when the parsed result is positive zero.
    private static ConfigValue ConvertDecimalFloat(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) switch
        {
            true when d == 0.0 && IsNegative(value) && BitConverter.DoubleToInt64Bits(d) >= 0 =>
                ConfigValue.Double(-d),
            true => ConfigValue.Double(d),

            // A false return means overflow for resolver-validated forms; .NET Core never reaches here.
            false when IsNegative(value) => ConfigValue.Double(double.NegativeInfinity),
            false => ConfigValue.Double(double.PositiveInfinity),
        };

    private static bool IsNegative(string value) =>
        value is { Length: > 0 } && value[0] == '-';

    private static ConfigValue AccumulateUnsigned(string value, int start, int radix)
    {
        var accumulator = 0UL;
        for (var i = start; i < value.Length; i++)
        {
            var digit = (ulong)DigitValue(value[i], radix);
            if (accumulator > (ulong.MaxValue - digit) / (ulong)radix)
            {
                return ConfigValue.UnrepresentableInteger();
            }

            accumulator = (accumulator * (ulong)radix) + digit;
        }

        return accumulator > (ulong)long.MaxValue
            ? ConfigValue.UnrepresentableInteger()
            : ConfigValue.Integer((long)accumulator);
    }

    private static double AccumulateDouble(string value, int start, int radix)
    {
        var accumulator = 0.0;
        for (var i = start; i < value.Length; i++)
        {
            accumulator = (accumulator * radix) + DigitValue(value[i], radix);
        }

        return accumulator;
    }

    private static int DigitValue(char c, int radix)
    {
        var digit = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => throw new InvalidOperationException($"Not a valid digit: '{c}'."),
        };

        if (digit >= radix)
        {
            var form = radix == 16 ? "hexadecimal" : "octal";
            throw new InvalidOperationException($"Digit '{c}' is not valid in {form} integer text.");
        }

        return digit;
    }
}
