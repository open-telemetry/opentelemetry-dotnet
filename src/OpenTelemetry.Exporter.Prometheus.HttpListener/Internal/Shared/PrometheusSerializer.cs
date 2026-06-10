// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Buffers;
using System.Buffers.Text;
#if NET9_0_OR_GREATER
using System.Collections.Frozen;
#else
using System.Collections.Immutable;
#endif
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
    // Matches the 100 MiB cap applied to the main scrape response buffer in
    // PrometheusCollectionManager. The serialized tags are ultimately copied into
    // that buffer, so they can never usefully exceed this size. Capping growth in
    // SerializeTags prevents an attacker-influenced, oversized histogram label value
    // from forcing unbounded scratch-buffer allocations during a scrape.
    internal const int MaxSerializedTagsBufferSize = 100 * 1024 * 1024;

#pragma warning disable SA1310 // Field name should not contain an underscore
    private const byte ASCII_QUOTATION_MARK = 0x22; // '"'
    private const byte ASCII_REVERSE_SOLIDUS = 0x5C; // '\\'
    private const byte ASCII_LINEFEED = 0x0A; // `\n`
#pragma warning restore SA1310 // Field name should not contain an underscore

    private const int MaxExemplarLabelSetCharacters = 128;

#if NET
    private static readonly SearchValues<char> UnicodeEscapeChars = SearchValues.Create("\\\n");
    private static readonly SearchValues<char> LabelValueEscapeChars = SearchValues.Create("\"\\\n");
#endif

#if NET9_0_OR_GREATER
    private static readonly FrozenSet<string> ReservedScopeLabelNames = FrozenSet.Create(["otel_scope_name", "otel_scope_schema_url", "otel_scope_version"]);
#elif NET
    private static readonly ImmutableHashSet<string> ReservedScopeLabelNames = ["otel_scope_name", "otel_scope_schema_url", "otel_scope_version"];
#else
    private static readonly HashSet<string> ReservedScopeLabelNames = ["otel_scope_name", "otel_scope_schema_url", "otel_scope_version"];
    private static readonly long UnixEpochTicks = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;
#endif

    private static readonly string[] ReservedExemplarLabelNames = ["trace_id", "span_id"];
    private static readonly string[] ReservedHistogramLabelNames = ["le"];
    private static readonly double[] ExactPowersOfTen =
    [
        1e-10d,
        1e-09d,
        1e-08d,
        1e-07d,
        1e-06d,
        1e-05d,
        1e-04d,
        1e-03d,
        1e-02d,
        1e-01d,
        1e00d,
        1e01d,
        1e02d,
        1e03d,
        1e04d,
        1e05d,
        1e06d,
        1e07d,
        1e08d,
        1e09d,
        1e10d,
    ];

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
    public static int WriteUnsignedLong(byte[] buffer, int cursor, ulong value)
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
        => WriteEscapedString(buffer, cursor, value, escapeQuotationMarks: false);

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
                return WriteCanonicalLabelValue(buffer, cursor, floatValue);

            case double doubleValue:
                return WriteCanonicalLabelValue(buffer, cursor, doubleValue);

            case decimal decimalValue:
#if NET
                var result = Utf8Formatter.TryFormat(decimalValue, buffer.AsSpan(cursor), out var bytesWritten);
                return AdvanceCursorOrThrow(result, cursor, bytesWritten);
#else
                return WriteLabelValue(buffer, cursor, decimalValue.ToString(CultureInfo.InvariantCulture));
#endif

            case IFormattable formattableValue:
                return WriteLabelValue(buffer, cursor, formattableValue.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty);

            default:
                return WriteLabelValue(buffer, cursor, value.ToString() ?? string.Empty);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteLabel(byte[] buffer, int cursor, string labelKey, object? labelValue, bool openMetricsRequested)
    {
        cursor = WriteLabelKey(buffer, cursor, labelKey, openMetricsRequested);
        return WriteSanitizedLabel(buffer, cursor, labelValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMetricName(byte[] buffer, int cursor, PrometheusMetric metric, bool openMetricsRequested)
        => WriteUtf8NoEscape(buffer, cursor, openMetricsRequested ? metric.OpenMetricsNameBytes : metric.NameBytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteMetricMetadataName(byte[] buffer, int cursor, PrometheusMetric metric, bool openMetricsRequested)
        => WriteUtf8NoEscape(buffer, cursor, openMetricsRequested ? metric.OpenMetricsMetadataNameBytes : metric.NameBytes);

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
        List<LabelData>? labels = null;

        if (exemplar.TraceId != default)
        {
            AddLabel("trace_id", exemplar.TraceId.ToHexString(), ref labels);
        }

        if (exemplar.SpanId != default)
        {
            AddLabel("span_id", exemplar.SpanId.ToHexString(), ref labels);
        }

        foreach (var tag in exemplar.FilteredTags)
        {
            AddLabel(tag.Key, tag.Value, ref labels, ReservedExemplarLabelNames);
        }

        cursor = WriteLabels(
            buffer,
            cursor,
            labels,
            writeEnclosingBraces: true,
            openMetricsRequested,
            maxLabelSetCharacters: MaxExemplarLabelSetCharacters);

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
        if (string.Equals(unit, metric.Unit, StringComparison.Ordinal) && metric.UnitBytes != null)
        {
            cursor = WriteUtf8NoEscape(buffer, cursor, metric.UnitBytes);
        }
        else
        {
#pragma warning disable IDE0370 // Remove unnecessary suppression
            for (var i = 0; i < unit!.Length; i++)
#pragma warning restore IDE0370 // Remove unnecessary suppression
            {
                var ordinal = (ushort)unit[i];
                buffer[cursor++] = unchecked((byte)ordinal);
            }
        }

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
        bool writeEnclosingBraces = true,
        IReadOnlyCollection<string>? reservedOutputKeys = null)
    {
        var startCursor = cursor;
        List<string>? writtenOutputKeys = null;
        var wroteLabel = false;

        if (writeEnclosingBraces)
        {
            buffer[cursor++] = unchecked((byte)'{');
        }

        if (TryWriteScopeLabels() &&
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

        foreach (var scopeLabel in CreateScopeLabelData(metric))
        {
            AddLabel(scopeLabel.OriginalKey, scopeLabel.OutputKey, scopeLabel.Value, ref labels, reservedOutputKeys);
        }

        foreach (var tag in tags)
        {
            AddLabel(tag.Key, tag.Value, ref labels, reservedOutputKeys);
        }

        return WriteLabels(buffer, cursor, labels, writeEnclosingBraces, openMetricsRequested);

        bool TryWriteScopeLabels()
        {
            foreach (var scopeLabel in CreateScopeLabels(metric))
            {
                if (!TryWriteLabel(scopeLabel.Key, scopeLabel.Value))
                {
                    return false;
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
            var outputKey = GetSanitizedLabelKey(key);

            if (reservedOutputKeys?.Contains(outputKey) == true)
            {
                return true;
            }

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

        using var attributes = resource.Attributes.GetEnumerator();
        if (!attributes.MoveNext())
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
        do
        {
            var attribute = attributes.Current;
            AddLabel(attribute.Key, attribute.Value, ref labels);
        }
        while (attributes.MoveNext());

        cursor = WriteLabels(buffer, cursor, labels, writeEnclosingBraces: true, openMetricsRequested);
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'1');
        buffer[cursor++] = ASCII_LINEFEED;

        return cursor;
    }

    private static string GetLabelValueString(object? labelValue) => labelValue switch
    {
        null => string.Empty,
        string stringValue => stringValue,
        bool booleanValue => booleanValue ? "true" : "false",
        sbyte signedByteValue => signedByteValue.ToString(CultureInfo.InvariantCulture),
        byte byteValue => byteValue.ToString(CultureInfo.InvariantCulture),
        short shortValue => shortValue.ToString(CultureInfo.InvariantCulture),
        ushort unsignedShortValue => unsignedShortValue.ToString(CultureInfo.InvariantCulture),
        int intValue => intValue.ToString(CultureInfo.InvariantCulture),
        uint unsignedIntValue => unsignedIntValue.ToString(CultureInfo.InvariantCulture),
        long longValue => longValue.ToString(CultureInfo.InvariantCulture),
        ulong unsignedLongValue => unsignedLongValue.ToString(CultureInfo.InvariantCulture),
        float floatValue => GetCanonicalLabelValueString(floatValue),
        double doubleValue => GetCanonicalLabelValueString(doubleValue),
        decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
        IFormattable formattableValue => formattableValue.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => labelValue.ToString() ?? string.Empty,
    };

    private static string NormalizeLabelKey(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "_";
        }

        var builder = new StringBuilder(value.Length + 1);
        var lastCharUnderscore = false;

        if (char.IsAsciiDigit(value[0]))
        {
            builder.Append('_');
            lastCharUnderscore = true;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (!IsAllowedMetricsLabelCharacter(ch))
            {
                if (!lastCharUnderscore)
                {
                    builder.Append('_');
                    lastCharUnderscore = true;
                }

                continue;
            }

            builder.Append(ch);
            lastCharUnderscore = ch == '_';
        }

        return builder.ToString();
    }

    private static List<KeyValuePair<string, string>> CreateScopeLabels(Metric metric)
    {
        var orderedKeys = new List<string>();
        var labelsByOutputKey = new Dictionary<string, List<LabelData>>(StringComparer.Ordinal);

        foreach (var label in CreateScopeLabelData(metric))
        {
            if (!labelsByOutputKey.TryGetValue(label.OutputKey, out var bucket))
            {
                bucket = [];
                labelsByOutputKey[label.OutputKey] = bucket;
                orderedKeys.Add(label.OutputKey);
            }

            bucket.Add(label);
        }

        var scopeLabels = new List<KeyValuePair<string, string>>(orderedKeys.Count);

        foreach (var key in orderedKeys)
        {
            scopeLabels.Add(new(key, GetMergedLabelValue(labelsByOutputKey[key])));
        }

        return scopeLabels;
    }

    private static List<LabelData> CreateScopeLabelData(Metric metric)
    {
        var scopeLabels = new List<LabelData>(3)
        {
            new("otel_scope_name", "otel_scope_name", GetLabelValueString(metric.MeterName)),
        };

        if (!string.IsNullOrEmpty(metric.MeterVersion))
        {
            scopeLabels.Add(new("otel_scope_version", "otel_scope_version", GetLabelValueString(metric.MeterVersion)));
        }

        if (!string.IsNullOrEmpty(metric.MeterSchemaUrl))
        {
            scopeLabels.Add(new("otel_scope_schema_url", "otel_scope_schema_url", GetLabelValueString(metric.MeterSchemaUrl)));
        }

        if (metric.MeterTags == null)
        {
            return scopeLabels;
        }

        foreach (var tag in metric.MeterTags)
        {
            var labelKey = NormalizeLabelKey($"otel_scope_{tag.Key}");

            if (ReservedScopeLabelNames.Contains(labelKey))
            {
                continue;
            }

            scopeLabels.Add(new(tag.Key, labelKey, GetLabelValueString(tag.Value)));
        }

        return scopeLabels;
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

#if NET
    private static int WriteEscapedString(byte[] buffer, int cursor, string value, bool escapeQuotationMarks)
        => WriteEscapedUtf8String(buffer, cursor, value.AsSpan(), escapeQuotationMarks ? LabelValueEscapeChars : UnicodeEscapeChars);

    private static int WriteUtf8NoEscape(byte[] buffer, int cursor, ReadOnlySpan<char> value)
    {
        var bytesRequired = Encoding.UTF8.GetByteCount(value);
        return bytesRequired > buffer.Length - cursor
            ? throw new ArgumentException("Destination buffer too small.", nameof(buffer))
            : cursor + Encoding.UTF8.GetBytes(value, buffer.AsSpan(cursor));
    }

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
    {
        for (var i = 0; i < value.Length; i++)
        {
            var ordinal = (ushort)value[i];
            switch (ordinal)
            {
                case ASCII_QUOTATION_MARK when escapeQuotationMarks:
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
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUtf8NoEscape(byte[] buffer, int cursor, ReadOnlySpan<byte> value)
    {
        if (value.Length > buffer.Length - cursor)
        {
            throw new ArgumentException("Destination buffer too small.", nameof(buffer));
        }

        value.CopyTo(buffer.AsSpan(cursor));
        return cursor + value.Length;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteSanitizedLabel(byte[] buffer, int cursor, object? labelValue)
    {
        buffer[cursor++] = unchecked((byte)'=');
        buffer[cursor++] = unchecked((byte)'"');

        // In Prometheus, a label with an empty label value is considered equivalent to a label that does not exist.
        cursor = WriteLabelValue(buffer, cursor, labelValue);
        buffer[cursor++] = unchecked((byte)'"');

        return cursor;
    }

    private static string GetSanitizedLabelKey(string value)
    {
        var builder = new StringBuilder(value.Length + 1);
        _ = WriteSanitizedLabelKey(null, 0, value, builder);
        return builder.ToString();
    }

    private static int WriteSanitizedLabelKey(byte[]? buffer, int cursor, string value, StringBuilder? builder)
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

            if (!IsAllowedMetricsLabelCharacter(ch))
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

    private static void AddLabel(string originalKey, object? value, ref List<LabelData>? labels, IReadOnlyCollection<string>? reservedOutputKeys = null)
        => AddLabel(originalKey, GetSanitizedLabelKey(originalKey), value, ref labels, reservedOutputKeys);

    private static void AddLabel(string originalKey, string outputKey, object? value, ref List<LabelData>? labels, IReadOnlyCollection<string>? reservedOutputKeys = null)
    {
        if (reservedOutputKeys?.Contains(outputKey) == true)
        {
            return;
        }

        labels ??= [];
        labels.Add(new LabelData(originalKey, outputKey, GetLabelValueString(value)));
    }

    private static int WriteLabels(
        byte[] buffer,
        int cursor,
        IReadOnlyList<LabelData>? labels,
        bool writeEnclosingBraces,
        bool openMetricsRequested,
        int? maxLabelSetCharacters = null)
    {
        if (writeEnclosingBraces)
        {
            buffer[cursor++] = unchecked((byte)'{');
        }

        var wroteLabel = false;
        var labelSetCharacters = 0;

        if (labels != null && labels.Count > 0)
        {
            List<string>? orderedKeys = null;
            Dictionary<string, List<LabelData>>? labelsBySanitizedKey = null;

            foreach (var label in labels)
            {
                orderedKeys ??= [];
                labelsBySanitizedKey ??= [];

                if (!labelsBySanitizedKey.TryGetValue(label.OutputKey, out var bucket))
                {
                    bucket = [];
                    labelsBySanitizedKey[label.OutputKey] = bucket;
                    orderedKeys.Add(label.OutputKey);
                }

                bucket.Add(label);
            }

            Debug.Assert(orderedKeys != null, $"{nameof(orderedKeys)} should not be null.");
            Debug.Assert(labelsBySanitizedKey != null, $"{nameof(labelsBySanitizedKey)} should not be null.");

            var orderedOutputKeys = orderedKeys!;
            var groupedLabels = labelsBySanitizedKey!;

            foreach (var key in orderedOutputKeys)
            {
                var value = GetMergedLabelValue(groupedLabels[key]);

                if (maxLabelSetCharacters is { } maxCharactersValue)
                {
                    var labelCharacters = GetUtf8CodePointCount(key) + GetUtf8CodePointCount(value);
                    if (labelSetCharacters + labelCharacters > maxCharactersValue)
                    {
                        continue;
                    }

                    labelSetCharacters += labelCharacters;
                }

                cursor = WriteLabel(buffer, cursor, key, value, openMetricsRequested);
                buffer[cursor++] = unchecked((byte)',');
                wroteLabel = true;
            }
        }

        if (writeEnclosingBraces)
        {
            if (wroteLabel)
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

    private static int GetUtf8CodePointCount(string value)
    {
        var count = 0;

        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i < value.Length - 1 && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }

            count++;
        }

        return count;
    }

    private static string GetMergedLabelValue(List<LabelData> labels)
    {
        if (labels.Count == 1)
        {
            return labels[0].Value;
        }

        // "String Attribute values are converted directly to Metric Attributes
        // [...] this [...] may cause different OpenTelemetry keys to map to the
        // same Prometheus key. In such cases, the values MUST be concatenated
        // together, separated by `;`, and ordered by the lexicographical order
        // of the original keys.
        // See https://opentelemetry.io/docs/specs/otel/compatibility/prometheus_and_openmetrics/#metric-attributes
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteUnixTimeSeconds(byte[] buffer, int cursor, DateTimeOffset value) =>
#if NET
        WriteDouble(buffer, cursor, (value.UtcDateTime.Ticks - DateTimeOffset.UnixEpoch.Ticks) / (double)TimeSpan.TicksPerSecond);
#else
        WriteDouble(buffer, cursor, (value.UtcDateTime.Ticks - UnixEpochTicks) / (double)TimeSpan.TicksPerSecond);
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
#endif

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
#if NET
            var minimumLength = formattedValue.IndexOf('.', StringComparison.Ordinal) + 2;
#else
            var minimumLength = formattedValue.IndexOf('.') + 2;
#endif
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

    private static bool TryGetPowerOfTenExponent(double absoluteValue, out int exponent)
    {
        exponent = 0;
        Debug.Assert(absoluteValue > 0, $"{nameof(absoluteValue)} should be positive.");

        var index = Array.IndexOf(ExactPowersOfTen, absoluteValue);
        if (index < 0)
        {
            return false;
        }

        exponent = index - 10;
        return true;
    }

    private readonly struct LabelData(string originalKey, string outputKey, string value)
    {
        public readonly string OriginalKey { get; } = originalKey;

        public readonly string OutputKey { get; } = outputKey;

        public readonly string Value { get; } = value;
    }
}
