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
            return AdvanceCursorOrThrow(result, cursor, bytesWritten);
#else
            return WriteAsciiStringNoEscape(buffer, cursor, value.ToString("G17", CultureInfo.InvariantCulture));
#endif
        }
        else if (double.IsPositiveInfinity(value))
        {
            return WriteAsciiStringNoEscape(buffer, cursor, "+Inf");
        }
        else if (double.IsNegativeInfinity(value))
        {
            return WriteAsciiStringNoEscape(buffer, cursor, "-Inf");
        }
        else
        {
            // See https://prometheus.io/docs/instrumenting/exposition_formats/#comments-help-text-and-type-information
            Debug.Assert(double.IsNaN(value), $"{nameof(value)} should be NaN.");
            return WriteAsciiStringNoEscape(buffer, cursor, "NaN");
        }
    }

    // Histogram "le" and summary "quantile" label values use OpenMetrics canonical numbers.
    // See https://prometheus.io/docs/specs/om/open_metrics_spec/#considerations-canonical-numbers
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteCanonicalLabelValue(byte[] buffer, int cursor, double value) =>
#if NET
        cursor + FormatCanonicalLabelValue(buffer.AsSpan(cursor), value);
#else
        WriteAsciiStringNoEscape(buffer, cursor, GetCanonicalLabelValueString(value));
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLong(byte[] buffer, int cursor, long value)
    {
#if NET
        var result = Utf8Formatter.TryFormat(value, buffer.AsSpan(cursor), out var bytesWritten);
        return AdvanceCursorOrThrow(result, cursor, bytesWritten);
#else
        return WriteAsciiStringNoEscape(buffer, cursor, value.ToString(CultureInfo.InvariantCulture));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteAsciiStringNoEscape(byte[] buffer, int cursor, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            buffer[cursor++] = unchecked((byte)value[i]);
        }

        return cursor;
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
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ordinal = (ushort)value[i];
            switch (ordinal)
            {
                case ASCII_REVERSE_SOLIDUS:
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    break;
                case ASCII_LINEFEED:
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = unchecked((byte)'n');
                    break;
                default:
                    cursor = WriteUnicodeScalar(buffer, cursor, value, ref i);
                    break;
            }
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLabelKey(byte[] buffer, int cursor, string value, bool openMetricsRequested)
    {
        if (string.IsNullOrEmpty(value))
        {
            buffer[cursor++] = unchecked((byte)'_');
            return cursor;
        }

        return openMetricsRequested ?
               WriteOpenMetricsLabelKey(buffer, cursor, value) :
               WritePrometheusLabelKey(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLabelValue(byte[] buffer, int cursor, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ordinal = (ushort)value[i];
            switch (ordinal)
            {
                case ASCII_QUOTATION_MARK:
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = ASCII_QUOTATION_MARK;
                    break;
                case ASCII_REVERSE_SOLIDUS:
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    break;
                case ASCII_LINEFEED:
                    buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                    buffer[cursor++] = unchecked((byte)'n');
                    break;
                default:
                    cursor = WriteUnicodeScalar(buffer, cursor, value, ref i);
                    break;
            }
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLabel(byte[] buffer, int cursor, string labelKey, object? labelValue, bool openMetricsRequested)
    {
        cursor = WriteLabelKey(buffer, cursor, labelKey, openMetricsRequested);
        buffer[cursor++] = unchecked((byte)'=');
        buffer[cursor++] = unchecked((byte)'"');

        // In Prometheus, a label with an empty label value is considered equivalent to a label that does not exist.
        cursor = WriteLabelValue(buffer, cursor, GetLabelValueString(labelValue));
        buffer[cursor++] = unchecked((byte)'"');

        return cursor;

        static string GetLabelValueString(object? labelValue)
        {
            // TODO: Attribute values should be written as their JSON representation. Extra logic may need to be added here to correctly convert other .NET types.
            // More detail: https://github.com/open-telemetry/opentelemetry-dotnet/issues/4822#issuecomment-1707328495
            if (labelValue is bool booleanValue)
            {
                return booleanValue ? "true" : "false";
            }
            else if (labelValue is double doubleValue)
            {
                return DoubleToString(doubleValue);
            }
            else if (labelValue is float floatValue)
            {
                return DoubleToString(floatValue);
            }

            return labelValue?.ToString() ?? string.Empty;

            static string DoubleToString(double value)
            {
                // From https://prometheus.io/docs/specs/om/open_metrics_spec/#considerations-canonical-numbers:
                // A warning to implementers in C and other languages that share its printf implementation:
                // The standard precision of %f, %e and %g is only six significant digits. 17 significant
                // digits are required for full precision, e.g. printf("%.17g", d).
                if (MathHelper.IsFinite(value))
                {
                    return value.ToString("G17", CultureInfo.InvariantCulture);
                }
                else if (double.IsPositiveInfinity(value))
                {
                    return "+Inf";
                }
                else if (double.IsNegativeInfinity(value))
                {
                    return "-Inf";
                }
                else
                {
                    // See https://prometheus.io/docs/instrumenting/exposition_formats/#comments-help-text-and-type-information
                    Debug.Assert(double.IsNaN(value), $"{nameof(value)} should be NaN.");
                    return "NaN";
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMetricName(byte[] buffer, int cursor, PrometheusMetric metric, bool openMetricsRequested)
    {
        // Metric name has already been escaped.
        var name = openMetricsRequested ? metric.OpenMetricsName : metric.Name;

        Debug.Assert(!string.IsNullOrWhiteSpace(name), "name was null or whitespace");

        for (var i = 0; i < name.Length; i++)
        {
            var ordinal = (ushort)name[i];
            buffer[cursor++] = unchecked((byte)ordinal);
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMetricMetadataName(byte[] buffer, int cursor, PrometheusMetric metric, bool openMetricsRequested)
    {
        // Metric name has already been escaped.
        var name = openMetricsRequested ? metric.OpenMetricsMetadataName : metric.Name;

        Debug.Assert(!string.IsNullOrWhiteSpace(name), "name was null or whitespace");

        for (var i = 0; i < name.Length; i++)
        {
            var ordinal = (ushort)name[i];
            buffer[cursor++] = unchecked((byte)ordinal);
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteEof(byte[] buffer, int cursor)
    {
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# EOF");
        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteExemplar(byte[] buffer, int cursor, in Exemplar exemplar, bool isLongValue, bool openMetricsRequested)
    {
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'#');
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'{');

        var hasLabels = false;

        if (exemplar.TraceId != default)
        {
            cursor = WriteLabel(buffer, cursor, "trace_id", exemplar.TraceId.ToHexString(), openMetricsRequested);
            buffer[cursor++] = unchecked((byte)',');
            hasLabels = true;
        }

        if (exemplar.SpanId != default)
        {
            cursor = WriteLabel(buffer, cursor, "span_id", exemplar.SpanId.ToHexString(), openMetricsRequested);
            buffer[cursor++] = unchecked((byte)',');
            hasLabels = true;
        }

        foreach (var tag in exemplar.FilteredTags)
        {
            if (tag.Key == "trace_id" || tag.Key == "span_id")
            {
                continue;
            }

            cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value, openMetricsRequested);
            buffer[cursor++] = unchecked((byte)',');
            hasLabels = true;
        }

        if (hasLabels)
        {
            buffer[cursor - 1] = unchecked((byte)'}');
        }
        else
        {
            buffer[cursor++] = unchecked((byte)'}');
        }

        buffer[cursor++] = unchecked((byte)' ');

        cursor = isLongValue
            ? WriteLong(buffer, cursor, exemplar.LongValue)
            : WriteDouble(buffer, cursor, exemplar.DoubleValue);

        if (exemplar.Timestamp != default)
        {
            buffer[cursor++] = unchecked((byte)' ');
            cursor = WriteUnixTimeSeconds(buffer, cursor, exemplar.Timestamp);
        }

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
        var metricType = MapPrometheusType(metric.Type, openMetricsRequested);

        Debug.Assert(!string.IsNullOrEmpty(metricType), $"{nameof(metricType)} should not be null or empty.");

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# TYPE ");
        cursor = WriteMetricMetadataName(buffer, cursor, metric, openMetricsRequested);
        buffer[cursor++] = unchecked((byte)' ');
        cursor = WriteAsciiStringNoEscape(buffer, cursor, metricType);

        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteUnitMetadata(byte[] buffer, int cursor, PrometheusMetric metric, string? unit, bool openMetricsRequested)
    {
        if (string.IsNullOrEmpty(unit))
        {
            return cursor;
        }

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# UNIT ");
        cursor = WriteMetricMetadataName(buffer, cursor, metric, openMetricsRequested);

        buffer[cursor++] = unchecked((byte)' ');

        // Unit name has already been escaped.
#pragma warning disable IDE0370 // Remove unnecessary suppression
        for (var i = 0; i < unit!.Length; i++)
#pragma warning restore IDE0370 // Remove unnecessary suppression
        {
            var ordinal = (ushort)unit[i];
            buffer[cursor++] = unchecked((byte)ordinal);
        }

        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteScopeInfo(byte[] buffer, int cursor, string scopeName, bool openMetricsRequested)
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
        cursor = WriteLabel(buffer, cursor, "otel_scope_name", scopeName, openMetricsRequested);
        buffer[cursor++] = unchecked((byte)'}');
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'1');
        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTags(
        byte[] buffer,
        int cursor,
        Metric metric,
        ReadOnlyTagCollection tags,
        bool openMetricsRequested,
        bool writeEnclosingBraces = true)
    {
        if (writeEnclosingBraces)
        {
            buffer[cursor++] = unchecked((byte)'{');
        }

        cursor = WriteLabel(buffer, cursor, "otel_scope_name", metric.MeterName, openMetricsRequested);
        buffer[cursor++] = unchecked((byte)',');

        if (!string.IsNullOrEmpty(metric.MeterVersion))
        {
            cursor = WriteLabel(buffer, cursor, "otel_scope_version", metric.MeterVersion, openMetricsRequested);
            buffer[cursor++] = unchecked((byte)',');
        }

        if (metric.MeterTags != null)
        {
            foreach (var tag in metric.MeterTags)
            {
                cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value, openMetricsRequested);
                buffer[cursor++] = unchecked((byte)',');
            }
        }

        foreach (var tag in tags)
        {
            cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value, openMetricsRequested);
            buffer[cursor++] = unchecked((byte)',');
        }

        if (writeEnclosingBraces)
        {
            buffer[cursor - 1] = unchecked((byte)'}'); // Note: We write the '}' over the last written comma, which is extra.
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTargetInfo(byte[] buffer, int cursor, Resource resource, bool openMetricsRequested)
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
            cursor = WriteLabel(buffer, cursor, attribute.Key, attribute.Value, openMetricsRequested);

            buffer[cursor++] = unchecked((byte)',');
        }

        cursor--; // Write over the last written comma

        buffer[cursor++] = unchecked((byte)'}');
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'1');
        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    private static int WritePrometheusLabelKey(byte[] buffer, int cursor, string value)
        => WriteNormalizedLabelKey(buffer, cursor, value);

    private static int WriteOpenMetricsLabelKey(byte[] buffer, int cursor, string value)
        => WriteNormalizedLabelKey(buffer, cursor, value);

    private static int WriteNormalizedLabelKey(byte[] buffer, int cursor, string value)
    {
        var lastCharUnderscore = false;

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            if (i == 0 && char.IsAsciiDigit(ch))
            {
                if (!lastCharUnderscore)
                {
                    buffer[cursor++] = unchecked((byte)'_');
                    lastCharUnderscore = true;
                }
            }

            if (!IsAllowedMetricsLabelCharacter(ch))
            {
                if (!lastCharUnderscore)
                {
                    buffer[cursor++] = unchecked((byte)'_');
                    lastCharUnderscore = true;
                }

                continue;
            }

            buffer[cursor++] = unchecked((byte)ch);
            lastCharUnderscore = ch == '_';
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAllowedMetricsLabelCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '_';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUnicodeScalar(byte[] buffer, int cursor, string value, ref int index)
    {
        // Strings MUST only consist of valid UTF-8 characters.
        // See https://prometheus.io/docs/specs/om/open_metrics_spec/#strings.
        var current = value[index];

        if (!char.IsSurrogate(current))
        {
            return WriteUnicodeNoEscape(buffer, cursor, current);
        }

        if (char.IsHighSurrogate(current) && index < value.Length - 1 && char.IsLowSurrogate(value[index + 1]))
        {
            index++;
            return WriteUnicodeNoEscape(buffer, cursor, char.ConvertToUtf32(current, value[index]));
        }

        return WriteUnicodeNoEscape(buffer, cursor, 0xFFFD);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUnixTimeSeconds(byte[] buffer, int cursor, DateTimeOffset value)
        => WriteDouble(buffer, cursor, value.ToUnixTimeMilliseconds() / 1000.0);

    private static string MapPrometheusType(PrometheusType type, bool openMetricsRequested) => type switch
    {
        PrometheusType.Gauge => "gauge",
        PrometheusType.Counter => "counter",
        PrometheusType.Summary => "summary",
        PrometheusType.Histogram => "histogram",

        // OpenMetrics 1.0 uses "unknown" while Prometheus text format 0.0.4 uses "untyped".
        // See https://prometheus.io/docs/specs/om/open_metrics_spec/#unknown-1
        PrometheusType.Untyped or _ => openMetricsRequested ? "unknown" : "untyped",
    };

#if NET
    private static int FormatCanonicalLabelValue(Span<byte> destination, double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return GetBytesWrittenOrThrow("+Inf"u8.TryCopyTo(destination), 4);
        }
        else if (double.IsNegativeInfinity(value))
        {
            return GetBytesWrittenOrThrow("-Inf"u8.TryCopyTo(destination), 4);
        }
        else if (double.IsNaN(value))
        {
            return GetBytesWrittenOrThrow("NaN"u8.TryCopyTo(destination), 3);
        }
        else if (value == 0)
        {
            return GetBytesWrittenOrThrow("0.0"u8.TryCopyTo(destination), 3);
        }

        var absoluteValue = Math.Abs(value);
        if (absoluteValue <= 10 && value == Math.Round(value, 3))
        {
            return FormatFixedAndTrim(destination, value, 3);
        }

        if (absoluteValue < 1e6 && value == Math.Round(value))
        {
            return FormatFixedAndTrim(destination, value, 1);
        }

        if (TryGetPowerOfTenExponent(absoluteValue, out var exponent))
        {
            return exponent is >= 6 or <= -5
                ? FormatPowerOfTenScientific(destination, value < 0, exponent)
                : FormatFixedAndTrim(destination, value, Math.Max(1, -exponent));
        }

        char symbol = absoluteValue >= 1e6 || absoluteValue < 1e-4 ? 'e' : 'G';

        return TryFormat(destination, value, new(symbol, 17));

        static int FormatFixedAndTrim(Span<byte> destination, double value, int decimalPlaces)
        {
            var bytesWritten = TryFormat(destination, value, new StandardFormat('F', (byte)decimalPlaces));
            var decimalIndex = destination.Slice(0, bytesWritten).IndexOf((byte)'.');
            Debug.Assert(decimalIndex >= 0, $"{nameof(decimalIndex)} should be non-negative.");

            while (bytesWritten > decimalIndex + 2 && destination[bytesWritten - 1] == (byte)'0')
            {
                bytesWritten--;
            }

            return bytesWritten;
        }

        static int FormatPowerOfTenScientific(Span<byte> destination, bool isNegative, int exponent)
        {
            if (destination.Length < (isNegative ? 6 : 5))
            {
                throw new ArgumentException("Destination buffer too small.");
            }

            var bytesWritten = 0;
            if (isNegative)
            {
                destination[bytesWritten++] = (byte)'-';
            }

            destination[bytesWritten++] = (byte)'1';
            return bytesWritten + WriteExponent(destination.Slice(bytesWritten), exponent);
        }

        static int TryFormat(Span<byte> destination, double value, StandardFormat format)
        {
            var result = Utf8Formatter.TryFormat(value, destination, out var bytesWritten, format);
            return GetBytesWrittenOrThrow(result, bytesWritten);
        }

        static int WriteExponent(Span<byte> destination, int exponent)
        {
            if (destination.Length < 4)
            {
                throw new ArgumentException("Destination buffer too small.");
            }

            destination[0] = (byte)'e';
            destination[1] = exponent >= 0 ? (byte)'+' : (byte)'-';

            var absoluteExponent = Math.Abs(exponent);
            (var quotient, var remainder) = Math.DivRem(absoluteExponent, 10);

            destination[2] = unchecked((byte)('0' + quotient));
            destination[3] = unchecked((byte)('0' + remainder));
            return 4;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBytesWrittenOrThrow(bool result, int bytesWritten) =>
        result ? bytesWritten : throw new ArgumentException("Destination buffer too small.");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AdvanceCursorOrThrow(bool result, int cursor, int bytesWritten) =>
        result ? cursor + bytesWritten : throw new ArgumentException("Destination buffer too small.");
#else
    private static string GetCanonicalLabelValueString(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return "+Inf";
        }
        else if (double.IsNegativeInfinity(value))
        {
            return "-Inf";
        }
        else if (double.IsNaN(value))
        {
            return "NaN";
        }
        else if (value == 0)
        {
            return "0.0";
        }

        var absoluteValue = Math.Abs(value);
        if (absoluteValue <= 10 && value == Math.Round(value, 3))
        {
            return FormatFixedAndTrim(value, 3);
        }

        if (absoluteValue < 1e6 && value == Math.Round(value))
        {
            return FormatFixedAndTrim(value, 1);
        }

        if (TryGetPowerOfTenExponent(absoluteValue, out var exponent))
        {
            return exponent is >= 6 or <= -5
                ? string.Concat(value < 0 ? "-1" : "1", FormatExponent(exponent))
                : FormatFixedAndTrim(value, Math.Max(1, -exponent));
        }

        return value.ToString(absoluteValue >= 1e6 || absoluteValue < 1e-4 ? "e17" : "G17", CultureInfo.InvariantCulture);

        static string FormatFixedAndTrim(double value, int decimalPlaces)
        {
            var formattedValue = value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
            var minimumLength = formattedValue.IndexOf('.') + 2;
            while (formattedValue.Length > minimumLength && formattedValue[formattedValue.Length - 1] == '0')
            {
                formattedValue = formattedValue.Substring(0, formattedValue.Length - 1);
            }

            return formattedValue;
        }

        static string FormatExponent(int exponent)
        {
            return string.Concat(
                "e",
                exponent >= 0 ? "+" : "-",
                Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture));
        }
    }
#endif

    private static bool TryGetPowerOfTenExponent(double absoluteValue, out int exponent)
    {
        exponent = 0;
        Debug.Assert(absoluteValue > 0, $"{nameof(absoluteValue)} should be positive.");

        var roundedExponent = (int)Math.Round(Math.Log10(absoluteValue));
        if (roundedExponent is < -10 or > 10)
        {
            return false;
        }

        var powerOfTen = Math.Pow(10, roundedExponent);

        if (Math.Abs(absoluteValue - powerOfTen) > powerOfTen * 1e-12)
        {
            return false;
        }

        exponent = roundedExponent;
        return true;
    }
}
