// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Resolves scalar nodes according to the YAML 1.2 core schema.
/// </summary>
internal static class Yaml12ScalarResolver
{
    internal const string StringTag = "tag:yaml.org,2002:str";
    internal const string BooleanTag = "tag:yaml.org,2002:bool";
    internal const string IntegerTag = "tag:yaml.org,2002:int";
    internal const string FloatTag = "tag:yaml.org,2002:float";
    internal const string NullTag = "tag:yaml.org,2002:null";

    /// <summary>
    /// Resolves a scalar using its presentation style, tag, and post-substitution value.
    /// </summary>
    /// <param name="scalar">The parsed scalar node.</param>
    /// <param name="substitutedValue">The exact value after environment substitution.</param>
    /// <returns>The resolved scalar.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when an explicit core tag has an invalid representation or when an unsupported
    /// explicit tag is used.
    /// </exception>
    internal static ResolvedYamlScalar Resolve(YamlScalarNode scalar, string substitutedValue)
    {
        var tag = scalar.Tag;
        if (tag.IsEmpty)
        {
            return IsPlain(scalar)
                ? ResolveImplicitPlain(substitutedValue)
                : new(substitutedValue, YamlScalarKind.String);
        }

        if (tag.IsNonSpecific)
        {
            // YAML 1.2 section 10.3.2: the bare "!" non-specific tag resolves a scalar to
            // !!str. The "?" non-specific tag represents normal implicit resolution.
            return string.Equals(tag.ToString(), "!", StringComparison.Ordinal)
                ? new(substitutedValue, YamlScalarKind.String)
                : IsPlain(scalar)
                    ? ResolveImplicitPlain(substitutedValue)
                    : new(substitutedValue, YamlScalarKind.String);
        }

        return ResolveExplicitTag(tag.Value, substitutedValue);
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="scalar"/> was written as a plain
    /// (unquoted) scalar, and is therefore subject to implicit type resolution.
    /// </summary>
    /// <param name="scalar">The scalar node to test.</param>
    /// <returns><see langword="true"/> for a plain scalar.</returns>
    /// <remarks>
    /// <see cref="ScalarStyle.Any"/> is deliberately excluded. It is the enum's default value rather
    /// than a style the parser ever assigns - every node produced by
    /// <see cref="YamlStream.Load(TextReader)"/> carries a concrete style - so treating it as plain
    /// would type-resolve a programmatically constructed node that never declared itself plain.
    /// Defaulting such a node to string is the safe direction.
    /// </remarks>
    internal static bool IsPlain(YamlScalarNode scalar) =>
        scalar.Style is ScalarStyle.Plain or ScalarStyle.ForcePlain;

    internal static bool IsNull(string value) =>
        value.Length == 0 || value is "~" or "null" or "Null" or "NULL";

    internal static bool IsBoolean(string value) =>
        value is "true" or "True" or "TRUE" or "false" or "False" or "FALSE";

    internal static bool TryGetBoolean(string value, out bool result)
    {
        switch (value)
        {
            case "true":
            case "True":
            case "TRUE":
                result = true;
                return true;
            case "false":
            case "False":
            case "FALSE":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    internal static bool IsInteger(string value)
    {
        if (IsDecimalInteger(value))
        {
            return true;
        }

        // YAML 1.2 core schema: only the decimal form accepts an optional sign. Octal and hex
        // are exactly "0o[0-7]+" and "0x[0-9a-fA-F]+", so "-0o7" / "+0x3A" are strings.
        if (value.Length < 3 || value[0] != '0')
        {
            return false;
        }

        return value[1] switch
        {
            'o' => IsDigitRun(value, 2, static c => c is >= '0' and <= '7'),
            'x' => IsDigitRun(value, 2, char.IsAsciiHexDigit),
            _ => false,
        };
    }

    internal static bool IsFloat(string value)
    {
        if (IsInfinity(value) || value is ".nan" or ".NaN" or ".NAN")
        {
            return true;
        }

        var i = HasSign(value) ? 1 : 0;
        if (i == value.Length)
        {
            return false;
        }

        var hasFraction = false;
        if (value[i] == '.')
        {
            hasFraction = true;
            i++;
            var fractionStart = i;
            ConsumeDecimalDigits(value, ref i);
            if (i == fractionStart)
            {
                return false;
            }
        }
        else
        {
            var integerStart = i;
            ConsumeDecimalDigits(value, ref i);
            if (i == integerStart)
            {
                return false;
            }

            if (i < value.Length && value[i] == '.')
            {
                hasFraction = true;
                i++;
                ConsumeDecimalDigits(value, ref i);
            }
        }

        var hasExponent = false;
        if (i < value.Length && value[i] is 'e' or 'E')
        {
            hasExponent = true;
            i++;
            if (i < value.Length && value[i] is '+' or '-')
            {
                i++;
            }

            var exponentStart = i;
            ConsumeDecimalDigits(value, ref i);
            if (i == exponentStart)
            {
                return false;
            }
        }

        return i == value.Length && (hasFraction || hasExponent);
    }

    private static ResolvedYamlScalar ResolveImplicitPlain(string value)
    {
        if (IsNull(value))
        {
            return new(value, YamlScalarKind.Null);
        }

        if (IsBoolean(value))
        {
            return new(value, YamlScalarKind.Boolean);
        }

        if (IsInteger(value))
        {
            return new(value, YamlScalarKind.Integer);
        }

        return IsFloat(value)
            ? new(value, YamlScalarKind.Float)
            : new(value, YamlScalarKind.String);
    }

    private static ResolvedYamlScalar ResolveExplicitTag(string tag, string value)
    {
        if (string.Equals(tag, StringTag, StringComparison.Ordinal))
        {
            return new(value, YamlScalarKind.String);
        }

        if (string.Equals(tag, NullTag, StringComparison.Ordinal))
        {
            return IsNull(value)
                ? new(value, YamlScalarKind.Null)
                : ThrowInvalidTaggedValue(tag, value);
        }

        if (string.Equals(tag, BooleanTag, StringComparison.Ordinal))
        {
            return IsBoolean(value)
                ? new(value, YamlScalarKind.Boolean)
                : ThrowInvalidTaggedValue(tag, value);
        }

        if (string.Equals(tag, IntegerTag, StringComparison.Ordinal))
        {
            return IsInteger(value)
                ? new(value, YamlScalarKind.Integer)
                : ThrowInvalidTaggedValue(tag, value);
        }

        if (string.Equals(tag, FloatTag, StringComparison.Ordinal))
        {
            // Explicit !!float accepts integer notation as well as the implicit float forms.
            return IsInteger(value) || IsFloat(value)
                ? new(value, YamlScalarKind.Float)
                : ThrowInvalidTaggedValue(tag, value);
        }

        throw new DeclarativeConfigurationException(
            $"YAML scalar value '{value}' uses unsupported explicit tag '{tag}'. " +
            "Declarative configuration accepts only YAML 1.2 core scalar tags.");
    }

    private static ResolvedYamlScalar ThrowInvalidTaggedValue(string tag, string value) =>
        throw new DeclarativeConfigurationException(
            $"YAML scalar value '{value}' is not a valid representation for explicit tag '{tag}'.");

    private static bool IsDecimalInteger(string value)
    {
        var i = HasSign(value) ? 1 : 0;
        if (i >= value.Length)
        {
            return false;
        }

        for (; i < value.Length; i++)
        {
            if (!char.IsAsciiDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInfinity(string value)
    {
        var i = HasSign(value) ? 1 : 0;
        if ((value.Length - i) != 4 || value[i] != '.')
        {
            return false;
        }

        var suffix = value.AsSpan(i + 1);
        return suffix.SequenceEqual("inf") || suffix.SequenceEqual("Inf") || suffix.SequenceEqual("INF");
    }

    private static bool HasSign(string value) =>
        value.Length > 0 && value[0] is '+' or '-';

    private static bool IsDigitRun(string value, int start, Func<char, bool> isDigit)
    {
        if (start >= value.Length)
        {
            return false;
        }

        for (var i = start; i < value.Length; i++)
        {
            if (!isDigit(value[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void ConsumeDecimalDigits(string value, ref int i)
    {
        while (i < value.Length && char.IsAsciiDigit(value[i]))
        {
            i++;
        }
    }
}
