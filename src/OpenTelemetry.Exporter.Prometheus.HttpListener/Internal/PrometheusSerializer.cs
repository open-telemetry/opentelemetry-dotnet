// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Buffers;
using System.Buffers.Text;
#endif
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Basic PrometheusSerializer which has no OpenTelemetry dependency.
/// </summary>
internal static partial class PrometheusSerializer
{
#if !NET
    private static readonly DateTimeOffset UnixEpoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
#endif
    private static readonly HashSet<string> ReservedScopeAttributeNames = ["name", "schema_url", "version"];

#pragma warning disable SA1310 // Field name should not contain an underscore
    private const byte ASCII_QUOTATION_MARK = 0x22; // '"'
    private const byte ASCII_REVERSE_SOLIDUS = 0x5C; // '\\'
    private const byte ASCII_LINEFEED = 0x0A; // `\n`
#pragma warning restore SA1310 // Field name should not contain an underscore

#if !NET
    private static readonly DateTimeOffset UnixEpoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
#endif
    private static readonly HashSet<string> ReservedScopeLabelNames = ["otel_scope_name", "otel_scope_schema_url", "otel_scope_version"];

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
        => WriteSanitizedLabelKey(buffer, cursor, value, openMetricsRequested, builder: null);

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
    public static int WriteExemplar(byte[] buffer, int cursor, in Exemplar exemplar, bool isLongValue)
    {
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'#');
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'{');

        var hasLabels = false;

        if (exemplar.TraceId != default)
        {
            cursor = WriteLabel(buffer, cursor, "trace_id", exemplar.TraceId.ToHexString());
            buffer[cursor++] = unchecked((byte)',');
            hasLabels = true;
        }

        if (exemplar.SpanId != default)
        {
            cursor = WriteLabel(buffer, cursor, "span_id", exemplar.SpanId.ToHexString());
            buffer[cursor++] = unchecked((byte)',');
            hasLabels = true;
        }

        foreach (var tag in exemplar.FilteredTags)
        {
            if (tag.Key == "trace_id" || tag.Key == "span_id")
            {
                continue;
            }

            cursor = WriteLabel(buffer, cursor, tag.Key, tag.Value);
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
    public static int WriteScopeInfo(byte[] buffer, int cursor, Metric metric)
    {
        cursor = WriteScopeInfoMetadata(buffer, cursor);
        return WriteScopeInfoMetric(buffer, cursor, metric);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteScopeInfoMetadata(byte[] buffer, int cursor)
    {
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# TYPE otel_scope info");
        buffer[cursor++] = ASCII_LINEFEED;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# HELP otel_scope Scope metadata");
        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteScopeInfoMetric(byte[] buffer, int cursor, Metric metric)
    {
        if (string.IsNullOrEmpty(metric.MeterName))
        {
            return cursor;
        }

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "otel_scope_info");
        cursor = WriteScopeLabels(buffer, cursor, metric, openMetricsRequested: true);
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
        var startCursor = cursor;
        List<string>? writtenOutputKeys = null;
        var wroteLabel = false;

        if (writeEnclosingBraces)
        {
            buffer[cursor++] = unchecked((byte)'{');
        }

        if (TryWriteLabel("otel_scope_name", metric.MeterName) &&
            (string.IsNullOrEmpty(metric.MeterVersion) || TryWriteLabel("otel_scope_version", metric.MeterVersion)) &&
            TryWriteMetricTags() &&
            TryWritePointTags())
        {
            if (writeEnclosingBraces)
            {
                buffer[cursor++] = unchecked((byte)'}');
            }
            else if (wroteLabel)
            {
                buffer[cursor++] = unchecked((byte)',');
            }

            return cursor;
        }

        cursor = startCursor;
        List<LabelData>? labels = null;
        AddScopeLabels(metric, openMetricsRequested, ref labels);

        foreach (var tag in tags)
        {
            AddLabel(tag.Key, tag.Value, openMetricsRequested, ref labels);
        }

        return WriteLabels(buffer, cursor, labels, openMetricsRequested, writeEnclosingBraces);

        bool TryWriteMetricTags()
        {
            if (metric.MeterTags != null)
            {
                foreach (var tag in metric.MeterTags)
                {
                    if (!TryWriteLabel(tag.Key, tag.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        bool TryWritePointTags()
        {
            foreach (var tag in tags)
            {
                if (!TryWriteLabel(tag.Key, tag.Value))
                {
                    return false;
                }
            }

            return true;
        }

        bool TryWriteLabel(string key, object? value)
        {
            var outputKey = GetSanitizedLabelKey(key, openMetricsRequested);

            if (writtenOutputKeys?.Contains(outputKey) == true)
            {
                return false;
            }

            writtenOutputKeys ??= [];
            writtenOutputKeys.Add(outputKey);

            if (wroteLabel)
            {
                buffer[cursor++] = unchecked((byte)',');
            }

            cursor = WriteLabel(buffer, cursor, key, value, openMetricsRequested);
            wroteLabel = true;

            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteTargetInfo(byte[] buffer, int cursor, Resource resource, bool openMetricsRequested)
    {
        if (resource == Resource.Empty)
        {
            return cursor;
        }

        // "If info-typed metric families are not yet supported...a gauge-typed metric
        // family named target_info with a constant value of 1 MUST be used instead.".
        // See https://opentelemetry.io/docs/specs/otel/compatibility/prometheus_and_openmetrics/#resource-attributes-1
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# TYPE ");
        cursor = WriteAsciiStringNoEscape(buffer, cursor, openMetricsRequested ? "target" : "target_info");
        buffer[cursor++] = unchecked((byte)' ');
        cursor = WriteAsciiStringNoEscape(buffer, cursor, openMetricsRequested ? "info" : "gauge");
        buffer[cursor++] = ASCII_LINEFEED;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# HELP ");
        cursor = WriteAsciiStringNoEscape(buffer, cursor, openMetricsRequested ? "target" : "target_info");
        cursor = WriteAsciiStringNoEscape(buffer, cursor, " Target metadata");
        buffer[cursor++] = ASCII_LINEFEED;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "target_info");
        List<LabelData>? labels = null;

        foreach (var attribute in resource.Attributes)
        {
            AddLabel(attribute.Key, attribute.Value, openMetricsRequested, ref labels);
        }

        cursor = WriteLabels(buffer, cursor, labels, openMetricsRequested, writeEnclosingBraces: true);
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

    private static int WriteScopeLabels(byte[] buffer, int cursor, Metric metric, bool openMetricsRequested)
    {
        List<LabelData>? labels = null;
        AddScopeLabels(metric, openMetricsRequested, ref labels);
        return WriteLabels(buffer, cursor, labels, openMetricsRequested, writeEnclosingBraces: true);
    }

    private static void AddScopeLabels(Metric metric, bool openMetricsRequested, ref List<LabelData>? labels)
    {
        AddLabel("otel_scope_name", "otel_scope_name", metric.MeterName, openMetricsRequested, ref labels);

        if (!string.IsNullOrEmpty(metric.MeterVersion))
        {
            AddLabel("otel_scope_version", "otel_scope_version", metric.MeterVersion, openMetricsRequested, ref labels);
        }

        if (!string.IsNullOrEmpty(metric.MeterSchemaUrl))
        {
            AddLabel("otel_scope_schema_url", "otel_scope_schema_url", metric.MeterSchemaUrl, openMetricsRequested, ref labels);
        }

        if (metric.MeterTags != null)
        {
            foreach (var tag in metric.MeterTags)
            {
                if (TryCreateScopeLabel(tag, openMetricsRequested, out var scopeLabel))
                {
                    labels ??= [];
                    labels.Add(scopeLabel);
                }
            }
        }
    }

    internal static string CreateScopeIdentity(Metric metric)
    {
        List<LabelData>? labels = null;
        AddScopeLabels(metric, openMetricsRequested: true, ref labels);
        var scopeLabels = MergeLabels(labels);
        scopeLabels.Sort(static (x, y) =>
        {
            var keyCompare = string.CompareOrdinal(x.Key, y.Key);
            return keyCompare != 0 ? keyCompare : string.CompareOrdinal(x.Value, y.Value);
        });

        var builder = new StringBuilder();

        foreach (var scopeLabel in scopeLabels)
        {
            builder.Append('\0')
                   .Append(scopeLabel.Key)
                   .Append('\0')
                   .Append(scopeLabel.Value);
        }

        return builder.ToString();
    }

    private static string GetLabelValueString(object? labelValue)
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
                Debug.Assert(double.IsNaN(value), $"{nameof(value)} should be NaN.");
                return "NaN";
            }
        }
    }

    private static string GetSanitizedLabelKey(string value, bool openMetricsRequested)
    {
        var builder = new StringBuilder(value.Length + 1);
        _ = WriteSanitizedLabelKey(null, 0, value, openMetricsRequested, builder);
        return builder.ToString();
    }

    private static int WriteSanitizedLabelKey(byte[]? buffer, int cursor, string value, bool openMetricsRequested, StringBuilder? builder)
    {
        if (string.IsNullOrEmpty(value))
        {
            return AppendSanitizedLabelKeyCharacter(buffer, cursor, builder, '_');
        }

        var lastCharUnderscore = false;

        if (char.IsAsciiDigit(value[0]))
        {
            cursor = AppendSanitizedLabelKeyCharacter(buffer, cursor, builder, '_');
            lastCharUnderscore = true;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            if (!IsAllowedMetricsLabelCharacter(ch, openMetricsRequested))
            {
                if (!lastCharUnderscore)
                {
                    cursor = AppendSanitizedLabelKeyCharacter(buffer, cursor, builder, '_');
                    lastCharUnderscore = true;
                }

                continue;
            }

            cursor = AppendSanitizedLabelKeyCharacter(buffer, cursor, builder, ch);
            lastCharUnderscore = ch == '_';
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int AppendSanitizedLabelKeyCharacter(byte[]? buffer, int cursor, StringBuilder? builder, char value)
    {
        if (buffer != null)
        {
            buffer[cursor++] = unchecked((byte)value);
        }
        else
        {
            builder!.Append(value);
        }

        return cursor;
    }

    private static void AddLabel(string originalKey, object? value, bool openMetricsRequested, ref List<LabelData>? labels)
        => AddLabel(originalKey, GetSanitizedLabelKey(originalKey, openMetricsRequested), value, openMetricsRequested, ref labels);

    private static void AddLabel(string originalKey, string outputKey, object? value, bool openMetricsRequested, ref List<LabelData>? labels)
    {
        labels ??= [];
        labels.Add(new LabelData(originalKey, GetSanitizedLabelKey(outputKey, openMetricsRequested), GetLabelValueString(value)));
    }

    private static List<KeyValuePair<string, string>> MergeLabels(IReadOnlyList<LabelData>? labels)
    {
        if (labels == null || labels.Count == 0)
        {
            return [];
        }

        List<string> orderedKeys = [];
        Dictionary<string, List<LabelData>> labelsBySanitizedKey = [];

        foreach (var label in labels)
        {
            if (!labelsBySanitizedKey.TryGetValue(label.OutputKey, out var bucket))
            {
                bucket = [];
                labelsBySanitizedKey[label.OutputKey] = bucket;
                orderedKeys.Add(label.OutputKey);
            }

            bucket.Add(label);
        }

        var mergedLabels = new List<KeyValuePair<string, string>>(orderedKeys.Count);

        foreach (var key in orderedKeys)
        {
            mergedLabels.Add(new(key, GetMergedLabelValue(labelsBySanitizedKey[key])));
        }

        return mergedLabels;
    }

    private static int WriteLabels(byte[] buffer, int cursor, IReadOnlyList<LabelData>? labels, bool openMetricsRequested, bool writeEnclosingBraces)
    {
        if (writeEnclosingBraces)
        {
            buffer[cursor++] = unchecked((byte)'{');
        }

        foreach (var label in MergeLabels(labels))
        {
            cursor = WriteLabel(buffer, cursor, label.Key, label.Value, openMetricsRequested);
            buffer[cursor++] = unchecked((byte)',');
        }

        if (writeEnclosingBraces)
        {
            if (labels != null && labels.Count > 0)
            {
                buffer[cursor - 1] = unchecked((byte)'}');
            }
            else
            {
                buffer[cursor++] = unchecked((byte)'}');
            }
        }

        return cursor;
    }

    private static string GetMergedLabelValue(List<LabelData> labels)
    {
        if (labels.Count == 1)
        {
            return labels[0].Value;
        }

        labels.Sort(static (left, right) => string.CompareOrdinal(left.OriginalKey, right.OriginalKey));
        var builder = new StringBuilder();

        for (var i = 0; i < labels.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            builder.Append(labels[i].Value);
        }

        return builder.ToString();
    }

    private static bool TryCreateScopeLabel(KeyValuePair<string, object?> tag, bool openMetricsRequested, out LabelData scopeLabel)
    {
        var labelKey = GetSanitizedLabelKey($"otel_scope_{tag.Key}", openMetricsRequested);

        if (ReservedScopeLabelNames.Contains(labelKey))
        {
            scopeLabel = default;
            return false;
        }

        scopeLabel = new(tag.Key, labelKey, GetLabelValueString(tag.Value));
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAllowedMetricsLabelCharacter(char value, bool isOpenMetrics) =>
        char.IsAsciiLetterOrDigit(value) || value is '_' || (isOpenMetrics && value == ':');

    private readonly struct LabelData(string originalKey, string outputKey, string value)
    {
        public readonly string OriginalKey { get; } = originalKey;

        public readonly string OutputKey { get; } = outputKey;

        public readonly string Value { get; } = value;
    }

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
    private static int WriteUnixTimeSeconds(byte[] buffer, int cursor, DateTimeOffset value) =>
#if NET
        WriteDouble(buffer, cursor, (value.UtcDateTime.Ticks - DateTimeOffset.UnixEpoch.Ticks) / (double)TimeSpan.TicksPerSecond);
#else
        WriteDouble(buffer, cursor, (value.UtcDateTime.Ticks - UnixEpoch.Ticks) / (double)TimeSpan.TicksPerSecond);
#endif

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
