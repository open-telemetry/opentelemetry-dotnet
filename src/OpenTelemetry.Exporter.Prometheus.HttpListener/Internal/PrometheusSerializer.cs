// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Buffers;
using System.Buffers.Text;
#endif
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Basic PrometheusSerializer which has no OpenTelemetry dependency.
/// </summary>
internal static partial class PrometheusSerializer
{
#pragma warning disable SA1310 // Field name should not contain an underscore
    private const byte ASCII_QUOTATION_MARK = 0x22; // '"'
    private const byte ASCII_REVERSE_SOLIDUS = 0x5C; // '\\'
    private const byte ASCII_LINEFEED = 0x0A; // `\n`
#pragma warning restore SA1310 // Field name should not contain an underscore

#if NET
    private static readonly SearchValues<char> UnicodeEscapeChars = SearchValues.Create("\\\n");
    private static readonly SearchValues<char> LabelValueEscapeChars = SearchValues.Create("\"\\\n");
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteDouble(byte[] buffer, int cursor, double value)
    {
        if (MathHelper.IsFinite(value))
        {
            // From https://prometheus.io/docs/specs/om/open_metrics_spec/#considerations-canonical-numbers:
            // A warning to implementers in C and other languages that share its printf implementation:
            // The standard precision of %f, %e and %g is only six significant digits. 17 significant
            // digits are required for full precision, e.g. printf("%.17g", d).
#if NET
            var result = Utf8Formatter.TryFormat(value, buffer.AsSpan(cursor), out var bytesWritten, new StandardFormat('G', 17));
            Debug.Assert(result, $"{nameof(result)} should be true.");

            cursor += bytesWritten;
#else
            cursor = WriteAsciiStringNoEscape(buffer, cursor, value.ToString("G17", CultureInfo.InvariantCulture));
#endif
        }
        else if (double.IsPositiveInfinity(value))
        {
            cursor = WriteAsciiStringNoEscape(buffer, cursor, "+Inf");
        }
        else if (double.IsNegativeInfinity(value))
        {
            cursor = WriteAsciiStringNoEscape(buffer, cursor, "-Inf");
        }
        else
        {
            // See https://prometheus.io/docs/instrumenting/exposition_formats/#comments-help-text-and-type-information
            Debug.Assert(double.IsNaN(value), $"{nameof(value)} should be NaN.");
            cursor = WriteAsciiStringNoEscape(buffer, cursor, "NaN");
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLong(byte[] buffer, int cursor, long value)
    {
#if NET
        var result = Utf8Formatter.TryFormat(value, buffer.AsSpan(cursor), out var bytesWritten);
        Debug.Assert(result, $"{nameof(result)} should be true.");

        cursor += bytesWritten;
#else
        cursor = WriteAsciiStringNoEscape(buffer, cursor, value.ToString(CultureInfo.InvariantCulture));
#endif

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnsignedLong(byte[] buffer, int cursor, ulong value)
    {
#if NET
        var result = Utf8Formatter.TryFormat(value, buffer.AsSpan(cursor), out var bytesWritten);
        Debug.Assert(result, $"{nameof(result)} should be true.");

        cursor += bytesWritten;
#else
        cursor = WriteAsciiStringNoEscape(buffer, cursor, value.ToString(CultureInfo.InvariantCulture));
#endif

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteAsciiStringNoEscape(byte[] buffer, int cursor, string value)
    {
#if NET
        return WriteUtf8NoEscape(buffer, cursor, value.AsSpan());
#else
        for (var i = 0; i < value.Length; i++)
        {
            buffer[cursor++] = unchecked((byte)value[i]);
        }

        return cursor;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnicodeNoEscape(byte[] buffer, int cursor, int ordinal)
    {
        // Strings MUST only consist of valid UTF-8 characters.
        // See https://prometheus.io/docs/specs/om/open_metrics_spec/#strings.
        if (ordinal <= 0x7F)
        {
            buffer[cursor++] = unchecked((byte)ordinal);
        }
        else if (ordinal <= 0x07FF)
        {
            buffer[cursor++] = unchecked((byte)(0b_1100_0000 | (ordinal >> 6)));
            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
        }
        else if (ordinal <= 0xFFFF)
        {
            buffer[cursor++] = unchecked((byte)(0b_1110_0000 | (ordinal >> 12)));
            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | ((ordinal >> 6) & 0b_0011_1111)));
            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
        }
        else
        {
            buffer[cursor++] = unchecked((byte)(0b_1111_0000 | (ordinal >> 18)));
            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | ((ordinal >> 12) & 0b_0011_1111)));
            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | ((ordinal >> 6) & 0b_0011_1111)));
            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnicodeString(byte[] buffer, int cursor, string value)
        => WriteEscapedString(buffer, cursor, value, escapeQuotationMarks: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLabelKey(byte[] buffer, int cursor, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            buffer[cursor++] = unchecked((byte)'_');
            return cursor;
        }

        if (char.IsAsciiDigit(value[0]))
        {
            buffer[cursor++] = unchecked((byte)'_');
        }

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            buffer[cursor++] = char.IsAsciiLetterOrDigit(ch) ? (byte)ch : (byte)'_';
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLabelValue(byte[] buffer, int cursor, string value)
        => WriteEscapedString(buffer, cursor, value, escapeQuotationMarks: true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLabelValue(byte[] buffer, int cursor, object? value)
    {
        switch (value)
        {
            case null:
                return cursor;

            case string stringValue:
                return WriteLabelValue(buffer, cursor, stringValue);

            case bool boolValue:
                return WriteAsciiStringNoEscape(buffer, cursor, boolValue ? "true" : "false");

            case sbyte signedByteValue:
                return WriteLong(buffer, cursor, signedByteValue);

            case byte byteValue:
                return WriteLong(buffer, cursor, byteValue);

            case short shortValue:
                return WriteLong(buffer, cursor, shortValue);

            case ushort unsignedShortValue:
                return WriteLong(buffer, cursor, unsignedShortValue);

            case int intValue:
                return WriteLong(buffer, cursor, intValue);

            case uint unsignedIntValue:
                return WriteLong(buffer, cursor, unsignedIntValue);

            case long longValue:
                return WriteLong(buffer, cursor, longValue);

            case ulong unsignedLongValue:
                return WriteUnsignedLong(buffer, cursor, unsignedLongValue);

            case float floatValue:
                return WriteDouble(buffer, cursor, floatValue);

            case double doubleValue:
                return WriteDouble(buffer, cursor, doubleValue);

            case decimal decimalValue:
#if NET
                var result = Utf8Formatter.TryFormat(decimalValue, buffer.AsSpan(cursor), out var bytesWritten);
                Debug.Assert(result, $"{nameof(result)} should be true.");
                return cursor + bytesWritten;
#else
                return WriteLabelValue(buffer, cursor, decimalValue.ToString(CultureInfo.InvariantCulture));
#endif

            case IFormattable formattableValue:
                return WriteLabelValue(buffer, cursor, formattableValue.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty);

            // TODO: Attribute values should be written as their JSON representation. Extra logic may need to be added here to correctly convert other .NET types.
            // More detail: https://github.com/open-telemetry/opentelemetry-dotnet/issues/4822#issuecomment-1707328495
            default:
                return WriteLabelValue(buffer, cursor, value.ToString() ?? string.Empty);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLabel(byte[] buffer, int cursor, string labelKey, object? labelValue)
    {
        cursor = WriteLabelKey(buffer, cursor, labelKey);
        buffer[cursor++] = unchecked((byte)'=');
        buffer[cursor++] = unchecked((byte)'"');

        // In Prometheus, a label with an empty label value is considered equivalent to a label that does not exist.
        cursor = WriteLabelValue(buffer, cursor, labelValue);
        buffer[cursor++] = unchecked((byte)'"');

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMetricName(byte[] buffer, int cursor, PrometheusMetric metric, bool openMetricsRequested)
        => WriteCachedMetricName(buffer, cursor, GetMetricName(metric, openMetricsRequested));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMetricMetadataName(byte[] buffer, int cursor, PrometheusMetric metric, bool openMetricsRequested)
        => WriteCachedMetricName(buffer, cursor, GetMetricMetadataName(metric, openMetricsRequested));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteEof(byte[] buffer, int cursor)
    {
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# EOF");
        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteHelpMetadata(byte[] buffer, int cursor, PrometheusMetric metric, string metricDescription, bool openMetricsRequested)
    {
        if (string.IsNullOrEmpty(metricDescription))
        {
            return cursor;
        }

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# HELP ");
        cursor = WriteMetricMetadataName(buffer, cursor, metric, openMetricsRequested);

        if (!string.IsNullOrEmpty(metricDescription))
        {
            buffer[cursor++] = unchecked((byte)' ');
            cursor = WriteUnicodeString(buffer, cursor, metricDescription);
        }

        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTypeMetadata(byte[] buffer, int cursor, PrometheusMetric metric, bool openMetricsRequested)
    {
        var metricType = MapPrometheusType(metric.Type);

        Debug.Assert(!string.IsNullOrEmpty(metricType), $"{nameof(metricType)} should not be null or empty.");

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# TYPE ");
        cursor = WriteMetricMetadataName(buffer, cursor, metric, openMetricsRequested);
        buffer[cursor++] = unchecked((byte)' ');
        cursor = WriteAsciiStringNoEscape(buffer, cursor, metricType);

        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnitMetadata(byte[] buffer, int cursor, PrometheusMetric metric, bool openMetricsRequested)
    {
        if (string.IsNullOrEmpty(metric.Unit))
        {
            return cursor;
        }

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# UNIT ");
        cursor = WriteMetricMetadataName(buffer, cursor, metric, openMetricsRequested);

        buffer[cursor++] = unchecked((byte)' ');

        cursor = WriteUtf8NoEscape(buffer, cursor, metric.UnitBytes!);

        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteScopeInfo(byte[] buffer, int cursor, string scopeName)
    {
        if (string.IsNullOrEmpty(scopeName))
        {
            return cursor;
        }

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# TYPE otel_scope_info info");
        buffer[cursor++] = ASCII_LINEFEED;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# HELP otel_scope_info Scope metadata");
        buffer[cursor++] = ASCII_LINEFEED;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "otel_scope_info");
        buffer[cursor++] = unchecked((byte)'{');
        cursor = WriteLabel(buffer, cursor, "otel_scope_name", scopeName);
        buffer[cursor++] = unchecked((byte)'}');
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'1');
        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTimestamp(byte[] buffer, int cursor, long value, bool useOpenMetrics)
    {
        if (useOpenMetrics)
        {
            cursor = WriteLong(buffer, cursor, value / 1000);
            buffer[cursor++] = unchecked((byte)'.');

            var millis = value % 1000;

            if (millis < 100)
            {
                buffer[cursor++] = unchecked((byte)'0');
            }

            if (millis < 10)
            {
                buffer[cursor++] = unchecked((byte)'0');
            }

            return WriteLong(buffer, cursor, millis);
        }

        return WriteLong(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTags(
        byte[] buffer,
        int cursor,
        PrometheusMetric? prometheusMetric,
        Metric metric,
        ReadOnlyTagCollection tags,
        bool writeEnclosingBraces = true)
    {
        if (writeEnclosingBraces)
        {
            buffer[cursor++] = unchecked((byte)'{');
        }

        cursor = prometheusMetric?.SerializedStaticTags != null
            ? WriteUtf8NoEscape(buffer, cursor, prometheusMetric.SerializedStaticTags)
            : WriteStaticTags(buffer, cursor, metric);

        foreach (var tag in tags)
        {
            cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value);
            buffer[cursor++] = unchecked((byte)',');
        }

        if (writeEnclosingBraces)
        {
            buffer[cursor - 1] = unchecked((byte)'}'); // Note: We write the '}' over the last written comma, which is extra.
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTargetInfo(byte[] buffer, int cursor, Resource resource)
    {
        if (resource == Resource.Empty)
        {
            return cursor;
        }

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# TYPE target info");
        buffer[cursor++] = ASCII_LINEFEED;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# HELP target Target metadata");
        buffer[cursor++] = ASCII_LINEFEED;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "target_info");
        buffer[cursor++] = unchecked((byte)'{');

        foreach (var attribute in resource.Attributes)
        {
            cursor = WriteLabel(buffer, cursor, attribute.Key, attribute.Value);

            buffer[cursor++] = unchecked((byte)',');
        }

        cursor--; // Write over the last written comma

        buffer[cursor++] = unchecked((byte)'}');
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'1');
        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteCachedMetricName(byte[] buffer, int cursor, KeyValuePair<string, byte[]> name)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(name.Key), "name was null or whitespace");
        return WriteUtf8NoEscape(buffer, cursor, name.Value);
    }

    private static int WriteUtf8NoEscape(byte[] buffer, int cursor, ReadOnlySpan<byte> value)
    {
        value.CopyTo(buffer.AsSpan(cursor));
        return cursor + value.Length;
    }

    private static KeyValuePair<string, byte[]> GetMetricName(PrometheusMetric metric, bool openMetricsRequested) =>
        openMetricsRequested
            ? new(metric.OpenMetricsName, metric.OpenMetricsNameBytes)
            : new(metric.Name, metric.NameBytes);

    private static KeyValuePair<string, byte[]> GetMetricMetadataName(PrometheusMetric metric, bool openMetricsRequested) =>
        openMetricsRequested
            ? new(metric.OpenMetricsMetadataName, metric.OpenMetricsMetadataNameBytes)
            : new(metric.Name, metric.NameBytes);

    private static int GetUnicodeOrdinal(ReadOnlySpan<char> value, out int charsConsumed)
    {
        const int UnicodeReplacementCharacter = 0xFFFD;

        var character = value[0];

        if (char.IsHighSurrogate(character))
        {
            if (value.Length > 1 && char.IsLowSurrogate(value[1]))
            {
                charsConsumed = 2;
                return char.ConvertToUtf32(character, value[1]);
            }

            charsConsumed = 1;
            return UnicodeReplacementCharacter;
        }

        if (char.IsLowSurrogate(character))
        {
            charsConsumed = 1;
            return UnicodeReplacementCharacter;
        }

        charsConsumed = 1;
        return character;
    }

#if NET
    private static int WriteEscapedString(byte[] buffer, int cursor, string value, bool escapeQuotationMarks)
        => WriteEscapedUtf8String(buffer, cursor, value.AsSpan(), escapeQuotationMarks ? LabelValueEscapeChars : UnicodeEscapeChars);

    private static int WriteUtf8NoEscape(byte[] buffer, int cursor, ReadOnlySpan<char> value)
        => cursor + System.Text.Encoding.UTF8.GetBytes(value, buffer.AsSpan(cursor));

    private static int WriteEscapedUtf8String(byte[] buffer, int cursor, ReadOnlySpan<char> value, SearchValues<char> escapedChars)
    {
        while (!value.IsEmpty)
        {
            var escapedIndex = value.IndexOfAny(escapedChars);
            var nonAsciiIndex = value.IndexOfAnyExceptInRange((char)0x00, (char)0x7F);

            var specialIndex =
                escapedIndex < 0 ? nonAsciiIndex
                : nonAsciiIndex < 0 ? escapedIndex
                : Math.Min(escapedIndex, nonAsciiIndex);

            if (specialIndex < 0)
            {
                return WriteUtf8NoEscape(buffer, cursor, value);
            }

            if (specialIndex > 0)
            {
                cursor = WriteUtf8NoEscape(buffer, cursor, value[..specialIndex]);
                value = value[specialIndex..];
            }

            switch (value[0])
            {
                case '"':
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = ASCII_QUOTATION_MARK;
                    value = value[1..];
                    break;
                case '\\':
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    value = value[1..];
                    break;
                case '\n':
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = unchecked((byte)'n');
                    value = value[1..];
                    break;
                default:
                    cursor = WriteUnicodeNoEscape(buffer, cursor, GetUnicodeOrdinal(value, out var charsConsumed));
                    value = value[charsConsumed..];
                    break;
            }
        }

        return cursor;
    }
#else
    private static int WriteEscapedString(byte[] buffer, int cursor, string value, bool escapeQuotationMarks)
        => WriteEscapedUtf16String(buffer, cursor, value, escapeQuotationMarks);

    private static int WriteEscapedUtf16String(byte[] buffer, int cursor, string value, bool escapeQuotationMarks)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            switch (character)
            {
                case '"' when escapeQuotationMarks:
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = ASCII_QUOTATION_MARK;
                    break;
                case '\\':
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    break;
                case '\n':
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = unchecked((byte)'n');
                    break;
                default:
                    cursor = WriteUnicodeNoEscape(buffer, cursor, GetUnicodeOrdinal(value.AsSpan(i), out var charsConsumed));
                    i += charsConsumed - 1;
                    break;
            }
        }

        return cursor;
    }
#endif

    private static int WriteStaticTags(byte[] buffer, int cursor, Metric metric)
    {
        cursor = WriteLabel(buffer, cursor, "otel_scope_name", metric.MeterName);
        buffer[cursor++] = unchecked((byte)',');

        if (!string.IsNullOrEmpty(metric.MeterVersion))
        {
            cursor = WriteLabel(buffer, cursor, "otel_scope_version", metric.MeterVersion);
            buffer[cursor++] = unchecked((byte)',');
        }

        if (metric.MeterTags != null)
        {
            foreach (var tag in metric.MeterTags)
            {
                cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value);
                buffer[cursor++] = unchecked((byte)',');
            }
        }

        return cursor;
    }

#if NET
    private static bool TryWriteStaticTags(Span<byte> buffer, Metric metric, out int cursor)
    {
        cursor = 0;
        return TryWriteStaticTags(buffer, ref cursor, metric);
    }

    private static bool TryWriteStaticTags(Span<byte> buffer, ref int cursor, Metric metric)
    {
        if (!TryWriteLabel(buffer, ref cursor, "otel_scope_name", metric.MeterName) ||
            !TryWriteByte(buffer, ref cursor, unchecked((byte)',')))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(metric.MeterVersion) &&
            (!TryWriteLabel(buffer, ref cursor, "otel_scope_version", metric.MeterVersion) ||
             !TryWriteByte(buffer, ref cursor, unchecked((byte)','))))
        {
            return false;
        }

        if (metric.MeterTags != null)
        {
            foreach (var tag in metric.MeterTags)
            {
                if (!TryWriteLabel(buffer, ref cursor, tag.Key, tag.Value) ||
                    !TryWriteByte(buffer, ref cursor, unchecked((byte)',')))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryWriteTags(
        Span<byte> buffer,
        PrometheusMetric? prometheusMetric,
        Metric metric,
        ReadOnlyTagCollection tags,
        bool writeEnclosingBraces,
        out int cursor)
    {
        cursor = 0;

        if (writeEnclosingBraces && !TryWriteByte(buffer, ref cursor, unchecked((byte)'{')))
        {
            return false;
        }

        if (prometheusMetric?.SerializedStaticTags != null)
        {
            if (!TryWriteBytes(buffer, ref cursor, prometheusMetric.SerializedStaticTags))
            {
                return false;
            }
        }
        else if (!TryWriteStaticTags(buffer, ref cursor, metric))
        {
            return false;
        }

        foreach (var tag in tags)
        {
            if (!TryWriteLabel(buffer, ref cursor, tag.Key, tag.Value) ||
                !TryWriteByte(buffer, ref cursor, unchecked((byte)',')))
            {
                return false;
            }
        }

        if (writeEnclosingBraces)
        {
            buffer[cursor - 1] = unchecked((byte)'}');
        }

        return true;
    }

    private static bool TryWriteLabel(Span<byte> buffer, ref int cursor, string labelKey, object? labelValue) =>
        TryWriteLabelKey(buffer, ref cursor, labelKey) &&
        TryWriteByte(buffer, ref cursor, unchecked((byte)'=')) &&
        TryWriteByte(buffer, ref cursor, ASCII_QUOTATION_MARK) &&
        TryWriteLabelValue(buffer, ref cursor, labelValue) &&
        TryWriteByte(buffer, ref cursor, ASCII_QUOTATION_MARK);

    private static bool TryWriteLabelKey(Span<byte> buffer, ref int cursor, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return TryWriteByte(buffer, ref cursor, unchecked((byte)'_'));
        }

        if (char.IsAsciiDigit(value[0]) &&
            !TryWriteByte(buffer, ref cursor, unchecked((byte)'_')))
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            var sanitizedByte = char.IsAsciiLetterOrDigit(value[i]) ? (byte)ch : (byte)'_';

            if (!TryWriteByte(buffer, ref cursor, sanitizedByte))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryWriteLabelValue(Span<byte> buffer, ref int cursor, object? value)
    {
        switch (value)
        {
            case null:
                return true;

            case string stringValue:
                return TryWriteLabelValue(buffer, ref cursor, stringValue);

            case bool boolValue:
                return TryWriteAsciiStringNoEscape(buffer, ref cursor, boolValue ? "true" : "false");

            case sbyte signedByteValue:
                return TryWriteLong(buffer, ref cursor, signedByteValue);

            case byte byteValue:
                return TryWriteLong(buffer, ref cursor, byteValue);

            case short shortValue:
                return TryWriteLong(buffer, ref cursor, shortValue);

            case ushort unsignedShortValue:
                return TryWriteLong(buffer, ref cursor, unsignedShortValue);

            case int intValue:
                return TryWriteLong(buffer, ref cursor, intValue);

            case uint unsignedIntValue:
                return TryWriteLong(buffer, ref cursor, unsignedIntValue);

            case long longValue:
                return TryWriteLong(buffer, ref cursor, longValue);

            case ulong unsignedLongValue:
                return TryWriteUnsignedLong(buffer, ref cursor, unsignedLongValue);

            case float floatValue:
                return TryWriteDouble(buffer, ref cursor, floatValue);

            case double doubleValue:
                return TryWriteDouble(buffer, ref cursor, doubleValue);

            case decimal decimalValue:
                if (!Utf8Formatter.TryFormat(decimalValue, buffer[cursor..], out var bytesWritten))
                {
                    return false;
                }

                cursor += bytesWritten;
                return true;

            case IFormattable formattableValue:
                return TryWriteLabelValue(buffer, ref cursor, formattableValue.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty);

            // TODO: Attribute values should be written as their JSON representation. Extra logic may need to be added here to correctly convert other .NET types.
            // More detail: https://github.com/open-telemetry/opentelemetry-dotnet/issues/4822#issuecomment-1707328495
            default:
                return TryWriteLabelValue(buffer, ref cursor, value.ToString() ?? string.Empty);
        }
    }

    private static bool TryWriteLabelValue(Span<byte> buffer, ref int cursor, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ordinal = (ushort)value[i];
            switch (ordinal)
            {
                case ASCII_QUOTATION_MARK:
                    if (!TryWriteByte(buffer, ref cursor, ASCII_REVERSE_SOLIDUS) ||
                        !TryWriteByte(buffer, ref cursor, ASCII_QUOTATION_MARK))
                    {
                        return false;
                    }

                    break;
                case ASCII_REVERSE_SOLIDUS:
                    if (!TryWriteByte(buffer, ref cursor, ASCII_REVERSE_SOLIDUS) ||
                        !TryWriteByte(buffer, ref cursor, ASCII_REVERSE_SOLIDUS))
                    {
                        return false;
                    }

                    break;
                case ASCII_LINEFEED:
                    if (!TryWriteByte(buffer, ref cursor, ASCII_REVERSE_SOLIDUS) ||
                        !TryWriteByte(buffer, ref cursor, unchecked((byte)'n')))
                    {
                        return false;
                    }

                    break;
                default:
                    if (!TryWriteUnicodeNoEscape(buffer, ref cursor, ordinal))
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    private static bool TryWriteAsciiStringNoEscape(Span<byte> buffer, ref int cursor, string value)
    {
        if (value.Length > buffer.Length - cursor)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            buffer[cursor++] = unchecked((byte)value[i]);
        }

        return true;
    }

    private static bool TryWriteLong(Span<byte> buffer, ref int cursor, long value)
    {
        if (!Utf8Formatter.TryFormat(value, buffer[cursor..], out var bytesWritten))
        {
            return false;
        }

        cursor += bytesWritten;
        return true;
    }

    private static bool TryWriteUnsignedLong(Span<byte> buffer, ref int cursor, ulong value)
    {
        if (!Utf8Formatter.TryFormat(value, buffer[cursor..], out var bytesWritten))
        {
            return false;
        }

        cursor += bytesWritten;
        return true;
    }

    private static bool TryWriteDouble(Span<byte> buffer, ref int cursor, double value)
    {
        if (MathHelper.IsFinite(value))
        {
            if (!Utf8Formatter.TryFormat(value, buffer[cursor..], out var bytesWritten, new StandardFormat('G')))
            {
                return false;
            }

            cursor += bytesWritten;
            return true;
        }

        return TryWriteAsciiStringNoEscape(
            buffer,
            ref cursor,
            double.IsPositiveInfinity(value) ? "+Inf" : double.IsNegativeInfinity(value) ? "-Inf" : "NaN");
    }

    private static bool TryWriteUnicodeNoEscape(Span<byte> buffer, ref int cursor, ushort ordinal)
    {
        if (ordinal <= 0x7F)
        {
            return TryWriteByte(buffer, ref cursor, unchecked((byte)ordinal));
        }
        else if (ordinal <= 0x07FF)
        {
            return TryWriteByte(buffer, ref cursor, unchecked((byte)(0b_1100_0000 | (ordinal >> 6)))) &&
                   TryWriteByte(buffer, ref cursor, unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111))));
        }

        return TryWriteByte(buffer, ref cursor, unchecked((byte)(0b_1110_0000 | (ordinal >> 12)))) &&
               TryWriteByte(buffer, ref cursor, unchecked((byte)(0b_1000_0000 | ((ordinal >> 6) & 0b_0011_1111)))) &&
               TryWriteByte(buffer, ref cursor, unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111))));
    }

    private static bool TryWriteBytes(Span<byte> buffer, ref int cursor, ReadOnlySpan<byte> value)
    {
        if (value.Length > buffer.Length - cursor)
        {
            return false;
        }

        value.CopyTo(buffer[cursor..]);
        cursor += value.Length;

        return true;
    }

    private static bool TryWriteByte(Span<byte> buffer, ref int cursor, byte value)
    {
        if ((uint)cursor >= (uint)buffer.Length)
        {
            return false;
        }

        buffer[cursor++] = value;
        return true;
    }
#endif

    private static string MapPrometheusType(PrometheusType type) => type switch
    {
        PrometheusType.Gauge => "gauge",
        PrometheusType.Counter => "counter",
        PrometheusType.Summary => "summary",
        PrometheusType.Histogram => "histogram",
        PrometheusType.Untyped or _ => "untyped",
    };
}
