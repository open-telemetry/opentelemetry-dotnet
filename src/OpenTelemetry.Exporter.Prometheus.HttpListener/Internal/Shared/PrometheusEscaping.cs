// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Implements the Prometheus name escaping schemes.
/// </summary>
/// <remarks>
/// The transformations are faithful to <c>EscapeName</c> in
/// https://github.com/prometheus/common/blob/43de10cc658055c6b5e4d619edc917d5af493409/model/metric.go
/// so that a Prometheus client can reverse them.
/// </remarks>
internal static class PrometheusEscaping
{
    // Upper bound on the number of bytes a single UTF-16 character can expand to. "_dot_" is five
    // bytes for the dots scheme and a value-encoded basic-multilingual-plane character ("_FFFD_"
    // or "_xxxx_") is at most six bytes; a surrogate pair expands to at most eight bytes across
    // two characters, so six per character remains a safe bound.
    private const int MaxBytesPerCharacter = 6;

    public static EscapingScheme FromString(string? escaping) => escaping switch
    {
        PrometheusProtocol.AllowUtf8Escaping => EscapingScheme.AllowUtf8,
        PrometheusProtocol.DotsEscaping => EscapingScheme.Dots,
        PrometheusProtocol.ValuesEscaping => EscapingScheme.Values,
        _ => EscapingScheme.Underscores,
    };

    /// <summary>
    /// Escapes a fully-constructed metric or label name (including any unit and type-specific
    /// suffixes) according to the supplied scheme. The name MUST be escaped as a single unit
    /// so that the structural underscores introduced when building the name are doubled and
    /// can be reversed by a client.
    /// </summary>
    /// <param name="name">The name to escape.</param>
    /// <param name="scheme">The escaping scheme to apply.</param>
    /// <returns>The escaped name. Always a valid legacy (ASCII) name for the dots and values schemes.</returns>
    public static string EscapeName(string name, EscapingScheme scheme)
    {
        // The underscores scheme is handled by the OpenTelemetry sanitization, and the allow-utf-8
        // scheme keeps the name unchanged, so neither is escaped here.
        if (string.IsNullOrEmpty(name) || scheme is EscapingScheme.AllowUtf8 or EscapingScheme.Underscores)
        {
            return name;
        }

        if (scheme == EscapingScheme.Values && IsValidLegacyName(name))
        {
            return name;
        }

        var rented = ArrayPool<byte>.Shared.Rent((scheme == EscapingScheme.Values ? 3 : 0) + (name.Length * MaxBytesPerCharacter));

        try
        {
            var length = EscapeName(rented, 0, name, scheme);
#if NET
            return Encoding.ASCII.GetString(rented.AsSpan(0, length));
#else
            return Encoding.ASCII.GetString(rented, 0, length);
#endif
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Escapes a fully-constructed metric or label name directly into <paramref name="buffer"/>
    /// according to the supplied scheme, avoiding an intermediate string allocation. The scheme
    /// MUST be <see cref="EscapingScheme.Dots"/> or <see cref="EscapingScheme.Values"/>; the
    /// underscores scheme is handled using the pre-computed names.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="cursor">The current position in the buffer.</param>
    /// <param name="name">The name to escape.</param>
    /// <param name="scheme">The escaping scheme to apply.</param>
    /// <returns>The new cursor position after writing the (ASCII) escaped name.</returns>
    public static int EscapeName(byte[] buffer, int cursor, string name, EscapingScheme scheme)
    {
        Debug.Assert(scheme is EscapingScheme.Dots or EscapingScheme.Values, $"Unexpected escaping scheme: {scheme}");

        if (string.IsNullOrEmpty(name))
        {
            return cursor;
        }

        if (scheme == EscapingScheme.Values && IsValidLegacyName(name))
        {
            return WriteAscii(buffer, cursor, name);
        }

        if (scheme == EscapingScheme.Values)
        {
            cursor = WriteAscii(buffer, cursor, "U__");
        }

        var index = 0;

        while (index < name.Length)
        {
            var codePoint = GetCodePoint(name, index, out var charsConsumed, out var isValidRune);

            if (codePoint == '_')
            {
                cursor = WriteAscii(buffer, cursor, "__");
            }
            else if (scheme == EscapingScheme.Dots && codePoint == '.')
            {
                cursor = WriteAscii(buffer, cursor, "_dot_");
            }
            else if (IsValidLegacyRune(codePoint, index == 0))
            {
                buffer[cursor++] = unchecked((byte)codePoint);
            }
            else if (scheme == EscapingScheme.Dots)
            {
                cursor = WriteAscii(buffer, cursor, "__");
            }
            else if (!isValidRune)
            {
                cursor = WriteAscii(buffer, cursor, "_FFFD_");
            }
            else
            {
                cursor = WriteHexCodePoint(buffer, cursor, codePoint);
            }

            index += charsConsumed;
        }

        return cursor;
    }

    /// <summary>
    /// Returns whether the specified metric name matches the legacy metric name pattern <c>[a-zA-Z_:][a-zA-Z0-9_:]*</c>.
    /// </summary>
    /// <param name="name">The metric name to validate.</param>
    /// <returns>
    /// <see langword="true"/> if the name is a valid legacy metric name; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsValidLegacyName(string name) => IsValidLegacyName(name, allowColon: true);

    /// <summary>
    /// Returns whether the specified label name matches the legacy label name pattern <c>[a-zA-Z_][a-zA-Z0-9_]*</c>.
    /// </summary>
    /// <param name="name">The label name to validate.</param>
    /// <returns>
    /// <see langword="true"/> if the name is a valid legacy label name; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsValidLegacyLabelName(string name) => name.Length > 0 && IsValidLegacyName(name, allowColon: false);

    private static bool IsValidLegacyName(string name, bool allowColon)
    {
        if (name.Length == 0)
        {
            return false;
        }

        var index = 0;

        while (index < name.Length)
        {
            var codePoint = GetCodePoint(name, index, out var charsConsumed, out _);

            if (!IsValidLegacyRune(codePoint, index == 0) || (codePoint == ':' && !allowColon))
            {
                return false;
            }

            index += charsConsumed;
        }

        return true;
    }

    private static bool IsValidLegacyRune(int codePoint, bool isFirst) =>
        codePoint is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_' or ':' ||
        (!isFirst && codePoint is >= '0' and <= '9');

    private static int GetCodePoint(string value, int index, out int charsConsumed, out bool isValidRune)
    {
#if NET
        var status = Rune.DecodeFromUtf16(value.AsSpan(index), out var rune, out charsConsumed);

        isValidRune = status == OperationStatus.Done;

        return rune.Value;
#else
        const int UnicodeReplacementCharacter = 0xFFFD;

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
            // An unpaired surrogate is not a valid Unicode scalar value.
            charsConsumed = 1;
            isValidRune = false;
            return UnicodeReplacementCharacter;
        }

        charsConsumed = 1;
        isValidRune = true;

        return character;
#endif
    }

    private static int WriteAscii(byte[] buffer, int cursor, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            buffer[cursor++] = unchecked((byte)value[i]);
        }

        return cursor;
    }

    private static int WriteHexCodePoint(byte[] buffer, int cursor, int codePoint)
    {
        // Writes "_<lowercase-hex>_" with no leading zeros, matching the reference implementation's
        // use of strconv.FormatInt(value, 16). Code points are at most 0x10FFFF (six hex digits).
        buffer[cursor++] = unchecked((byte)'_');

        var started = false;

        for (var shift = 20; shift >= 0; shift -= 4)
        {
            var nibble = (codePoint >> shift) & 0xF;

            if (nibble != 0 || started || shift == 0)
            {
                buffer[cursor++] = unchecked((byte)(nibble < 10 ? '0' + nibble : 'a' + (nibble - 10)));
                started = true;
            }
        }

        buffer[cursor++] = unchecked((byte)'_');

        return cursor;
    }
}
