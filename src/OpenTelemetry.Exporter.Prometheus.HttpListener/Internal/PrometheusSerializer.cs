// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
            Span<char> span = stackalloc char[128];

            var result = value.TryFormat(span, out var cchWritten, "G17", CultureInfo.InvariantCulture);
            Debug.Assert(result, $"{nameof(result)} should be true.");

            for (var i = 0; i < cchWritten; i++)
            {
                buffer[cursor++] = unchecked((byte)span[i]);
            }
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
        Span<char> span = stackalloc char[20];

        var result = value.TryFormat(span, out var cchWritten, "G", CultureInfo.InvariantCulture);
        Debug.Assert(result, $"{nameof(result)} should be true.");

        for (var i = 0; i < cchWritten; i++)
        {
            buffer[cursor++] = unchecked((byte)span[i]);
        }
#else
        cursor = WriteAsciiStringNoEscape(buffer, cursor, value.ToString(CultureInfo.InvariantCulture));
#endif

        return cursor;
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

        // Unit name has already been escaped.
#pragma warning disable IDE0370 // Remove unnecessary suppression
        for (var i = 0; i < metric.Unit!.Length; i++)
#pragma warning restore IDE0370 // Remove unnecessary suppression
        {
            var ordinal = (ushort)metric.Unit[i];
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
        => WriteNormalizedLabelKey(buffer, cursor, value, isOpenMetrics: false);

    private static int WriteOpenMetricsLabelKey(byte[] buffer, int cursor, string value)
        => WriteNormalizedLabelKey(buffer, cursor, value, isOpenMetrics: true);

    private static int WriteNormalizedLabelKey(byte[] buffer, int cursor, string value, bool isOpenMetrics)
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

            if (!IsAllowedMetricsLabelCharacter(ch, isOpenMetrics))
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
    private static bool IsAllowedMetricsLabelCharacter(char value, bool isOpenMetrics) =>
        char.IsAsciiLetterOrDigit(value) || value is '_' || (isOpenMetrics && value == ':');

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

    private static string MapPrometheusType(PrometheusType type) => type switch
    {
        PrometheusType.Gauge => "gauge",
        PrometheusType.Counter => "counter",
        PrometheusType.Summary => "summary",
        PrometheusType.Histogram => "histogram",
        PrometheusType.Untyped or _ => "untyped",
    };
}
