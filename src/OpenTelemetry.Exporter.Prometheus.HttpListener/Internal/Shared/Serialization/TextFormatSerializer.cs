// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Buffers.Text;
#if NET9_0_OR_GREATER
using System.Collections.Frozen;
#else
using System.Collections.Immutable;
#endif
#endif
using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// Base class for serializing metrics in one of the Prometheus exposition text formats.
/// </summary>
/// <remarks>
/// This type carries all of the format-independent serialization logic. The points where the
/// Prometheus text format and the OpenMetrics format diverge are expressed as abstract members
/// so that the concrete format is selected once (via <see cref="GetSerializer"/>) and the rest
/// of the serialization uses dynamic dispatch.
/// </remarks>
internal abstract class TextFormatSerializer
{
    // Matches the 100 MiB cap applied to the main scrape response buffer in
    // PrometheusCollectionManager. The serialized tags are ultimately copied into
    // that buffer, so they can never usefully exceed this size. Capping growth in
    // SerializeTags prevents an attacker-influenced, oversized histogram label value
    // from forcing unbounded scratch-buffer allocations during a scrape.
    internal const int MaxSerializedTagsBufferSize = 100 * 1024 * 1024;

    protected const byte AsciiQuotationMark = 0x22; // '"'
    protected const byte AsciiReverseSolidus = 0x5C; // '\\'
    protected const byte AsciiLineFeed = 0x0A; // `\n`

    protected const int MaxExemplarLabelSetCharacters = 128;

    protected static readonly string[] ReservedHistogramLabelNames = ["le"];

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

    public static OpenMetricsV0Serializer OpenMetricsV0 => field ??= new();

    public static OpenMetricsV1Serializer OpenMetricsV1 => field ??= new();

    public static PrometheusTextV0Serializer PrometheusV0 => field ??= new();

    public static PrometheusTextV1Serializer PrometheusV1 => field ??= new();

    /// <summary>
    /// Gets the name escaping scheme to use for serialization.
    /// </summary>
    public EscapingScheme Escaping { get; private init; } = EscapingScheme.Underscores;

    /// <summary>
    /// Gets the type metadata value written for metrics that have no dedicated Prometheus type.
    /// </summary>
    protected abstract string UnknownMetricTypeName { get; }

    /// <summary>
    /// Gets the metric family name written for the target information metric.
    /// </summary>
    protected abstract string TargetInfoTypeName { get; }

    /// <summary>
    /// Gets the metric type written for the target information metric.
    /// </summary>
    protected abstract string TargetInfoTypeValue { get; }

    public static TextFormatSerializer GetSerializer(in PrometheusProtocol protocol)
    {
        var escaping = protocol.EscapingScheme;

        return protocol switch
        {
            { IsOpenMetrics: true } => protocol.Version.Major switch
            {
                0 => OpenMetricsV0,
                1 => escaping == EscapingScheme.Underscores ? OpenMetricsV1 : new OpenMetricsV1Serializer() { Escaping = escaping },
                _ => throw new NotSupportedException($"Unsupported OpenMetrics version: {protocol.Version}."),
            },
            { IsOpenMetrics: false } => protocol.Version.Major switch
            {
                0 => PrometheusV0,
                1 => escaping == EscapingScheme.Underscores ? PrometheusV1 : new PrometheusTextV1Serializer() { Escaping = escaping },
                _ => throw new NotSupportedException($"Unsupported Prometheus version: {protocol.Version}."),
            },
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int WriteEof(byte[] buffer, int cursor)
    {
        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# EOF");
        buffer[cursor++] = AsciiLineFeed;

        return cursor;
    }

    public int WriteMetric(
        byte[] buffer,
        int cursor,
        Metric metric,
        PrometheusMetric prometheusMetric,
        bool writeType,
        bool writeUnit,
        bool writeHelp,
        string? unitOverride,
        string? helpOverride)
    {
        if (writeType)
        {
            cursor = this.WriteTypeMetadata(buffer, cursor, prometheusMetric);
        }

        if (writeUnit)
        {
            cursor = this.WriteUnitMetadata(buffer, cursor, prometheusMetric, unitOverride ?? prometheusMetric.Unit);
        }

        if (writeHelp)
        {
            cursor = this.WriteHelpMetadata(buffer, cursor, prometheusMetric, helpOverride ?? metric.Description);
        }

        if (!metric.MetricType.IsHistogram())
        {
            var isLongValue = ((int)metric.MetricType & 0b_0000_1111) == 0x0a; // I8

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                // Counter and Gauge
                cursor = this.WriteSeriesAndTags(buffer, cursor, metric, prometheusMetric, metricPoint.Tags, suffix: null, reservedOutputKeys: null);

                buffer[cursor++] = unchecked((byte)' ');

                if (isLongValue)
                {
                    cursor = metric.MetricType.IsSum()
                        ? WriteLong(buffer, cursor, metricPoint.GetSumLong())
                        : WriteLong(buffer, cursor, metricPoint.GetGaugeLastValueLong());
                }
                else
                {
                    cursor = metric.MetricType.IsSum()
                        ? WriteDouble(buffer, cursor, metricPoint.GetSumDouble())
                        : WriteDouble(buffer, cursor, metricPoint.GetGaugeLastValueDouble());
                }

                cursor = this.WriteCounterExemplar(buffer, cursor, in metricPoint, prometheusMetric, isLongValue);

                buffer[cursor++] = AsciiLineFeed;

                cursor = this.WriteCounterCreated(buffer, cursor, metric, prometheusMetric, in metricPoint);
            }
        }
        else
        {
            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                var tags = metricPoint.Tags;
                var serializedTags = this.SerializeTags(metric, tags, ReservedHistogramLabelNames);
                var hasNegativeBucketBounds = false;
                var previousBound = double.NegativeInfinity;

                long totalCount = 0;
                foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                {
                    hasNegativeBucketBounds |= histogramMeasurement.ExplicitBound < 0;

                    totalCount += histogramMeasurement.BucketCount;

                    cursor = this.WriteHistogramBucketName(buffer, cursor, prometheusMetric);

                    cursor = WriteSerializedTagValues(buffer, cursor, serializedTags, appendTrailingComma: true);

                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "le=\"");

                    if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                    {
                        cursor = this.WriteExplicitBound(buffer, cursor, histogramMeasurement.ExplicitBound);
                    }
                    else
                    {
                        cursor = WriteAsciiStringNoEscape(buffer, cursor, "+Inf");
                    }

                    cursor = WriteAsciiStringNoEscape(buffer, cursor, "\"} ");

                    cursor = WriteLong(buffer, cursor, totalCount);

                    cursor = this.WriteHistogramBucketExemplar(buffer, cursor, in metricPoint, previousBound, histogramMeasurement.ExplicitBound);

                    buffer[cursor++] = AsciiLineFeed;
                    previousBound = histogramMeasurement.ExplicitBound;
                }

                if (this.ShouldWriteSumAndCount(hasNegativeBucketBounds))
                {
                    // OpenMetrics histograms with negative bucket thresholds MUST NOT expose
                    // _sum and therefore MUST NOT expose _count.
                    // See https://prometheus.io/docs/specs/om/open_metrics_spec/#histogram-1
                    cursor = this.WriteSeriesNameAndSerializedTags(buffer, cursor, prometheusMetric, "_sum", serializedTags);

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteDouble(buffer, cursor, metricPoint.GetHistogramSum());

                    buffer[cursor++] = AsciiLineFeed;

                    // Histogram count
                    cursor = this.WriteSeriesNameAndSerializedTags(buffer, cursor, prometheusMetric, "_count", serializedTags);

                    buffer[cursor++] = unchecked((byte)' ');

                    cursor = WriteLong(buffer, cursor, metricPoint.GetHistogramCount());
                    buffer[cursor++] = AsciiLineFeed;
                }

                cursor = this.WriteHistogramCreated(buffer, cursor, metric, prometheusMetric, in metricPoint);
            }
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int WriteTargetInfo(byte[] buffer, int cursor, Resource resource)
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
        cursor = WriteAsciiStringNoEscape(buffer, cursor, this.TargetInfoTypeName);
        buffer[cursor++] = unchecked((byte)' ');
        cursor = WriteAsciiStringNoEscape(buffer, cursor, this.TargetInfoTypeValue);
        buffer[cursor++] = AsciiLineFeed;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# HELP ");
        cursor = WriteAsciiStringNoEscape(buffer, cursor, this.TargetInfoTypeName);
        cursor = WriteAsciiStringNoEscape(buffer, cursor, " Target metadata");
        buffer[cursor++] = AsciiLineFeed;

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "target_info");
        List<LabelData>? labels = null;
        do
        {
            var attribute = attributes.Current;
            this.AddLabel(attribute.Key, attribute.Value, ref labels);
        }
        while (attributes.MoveNext());

        cursor = WriteLabels(buffer, cursor, labels, writeEnclosingBraces: true, default, null);
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'1');
        buffer[cursor++] = AsciiLineFeed;

        return cursor;
    }

    // The metadata family name (used for grouping and deduplicating metric metadata) for the
    // format being written. OpenMetrics drops the "_total" suffix from counters; the Prometheus
    // text format uses the sanitized name verbatim.
    public abstract string GetMetadataName(PrometheusMetric metric);

    internal static int GetNextSerializedTagsBufferSize(int currentBufferSize)
    {
        // Doubles the supplied buffer size, throwing once growth would exceed
        // MaxSerializedTagsBufferSize so that serializing an oversized tag set fails
        // fast instead of allocating without bound. An InvalidOperationException is
        // used deliberately: the buffer-growth retry loops in PrometheusCollectionManager
        // only retry on IndexOutOfRangeException/ArgumentException, so this terminates
        // the scrape immediately rather than repeatedly re-entering this allocation.
        var newBufferSize = currentBufferSize * 2;

        return newBufferSize <= 0 || newBufferSize > MaxSerializedTagsBufferSize
            ? throw new InvalidOperationException("The serialized Prometheus tag set exceeded the maximum supported size.")
            : newBufferSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteDouble(byte[] buffer, int cursor, double value)
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
    internal static int WriteCanonicalLabelValue(byte[] buffer, int cursor, double value) =>
#if NET
        cursor + FormatCanonicalLabelValue(buffer.AsSpan(cursor), value);
#else
        WriteAsciiStringNoEscape(buffer, cursor, GetCanonicalLabelValueString(value));
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLong(byte[] buffer, int cursor, long value)
    {
#if NET
        var result = Utf8Formatter.TryFormat(value, buffer.AsSpan(cursor), out var bytesWritten);
        return AdvanceCursorOrThrow(result, cursor, bytesWritten);
#else
        return WriteAsciiStringNoEscape(buffer, cursor, value.ToString(CultureInfo.InvariantCulture));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteUnsignedLong(byte[] buffer, int cursor, ulong value)
    {
#if NET
        var result = Utf8Formatter.TryFormat(value, buffer.AsSpan(cursor), out var bytesWritten);
        return AdvanceCursorOrThrow(result, cursor, bytesWritten);
#else
        return WriteAsciiStringNoEscape(buffer, cursor, value.ToString(CultureInfo.InvariantCulture));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteAsciiStringNoEscape(byte[] buffer, int cursor, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            buffer[cursor++] = unchecked((byte)value[i]);
        }

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteUnicodeNoEscape(byte[] buffer, int cursor, int ordinal)
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
    internal static int WriteUnicodeString(byte[] buffer, int cursor, string value)
        => WriteEscapedString(buffer, cursor, value, escapeQuotationMarks: false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLabelKey(byte[] buffer, int cursor, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            buffer[cursor++] = unchecked((byte)'_');
            return cursor;
        }

        return WriteNormalizedLabelKey(buffer, cursor, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLabelValue(byte[] buffer, int cursor, string value)
        => WriteEscapedString(buffer, cursor, value, escapeQuotationMarks: true);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLabelValue(byte[] buffer, int cursor, object? value)
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
    internal static int WriteLabel(byte[] buffer, int cursor, string labelKey, object? labelValue)
    {
        cursor = WriteLabelKey(buffer, cursor, labelKey);
        return WriteSanitizedLabel(buffer, cursor, labelValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteUnixTimeSeconds(byte[] buffer, int cursor, DateTimeOffset value) =>
#if NET
        WriteDouble(buffer, cursor, (value.UtcDateTime.Ticks - DateTimeOffset.UnixEpoch.Ticks) / (double)TimeSpan.TicksPerSecond);
#else
        WriteDouble(buffer, cursor, (value.UtcDateTime.Ticks - UnixEpochTicks) / (double)TimeSpan.TicksPerSecond);
#endif

    internal static int WriteSerializedTagValues(
        byte[] buffer,
        int cursor,
        ReadOnlySpan<byte> serializedTags,
        bool appendTrailingComma = false)
    {
        if (!serializedTags.IsEmpty)
        {
            if (serializedTags.Length > buffer.Length - cursor)
            {
                throw new ArgumentException("Destination buffer too small.", nameof(buffer));
            }

            serializedTags.CopyTo(buffer.AsSpan(cursor));
            cursor += serializedTags.Length;

            if (appendTrailingComma)
            {
                buffer[cursor++] = unchecked((byte)',');
            }
        }

        return cursor;
    }

    internal static int WriteSerializedTags(
        byte[] buffer,
        int cursor,
        ReadOnlySpan<byte> serializedTags,
        bool appendTrailingComma = false)
    {
        buffer[cursor++] = unchecked((byte)'{');
        cursor = WriteSerializedTagValues(buffer, cursor, serializedTags, appendTrailingComma);

        buffer[cursor++] = unchecked((byte)'}');
        return cursor;
    }

    internal static int WriteQuotedName(byte[] buffer, int cursor, ReadOnlySpan<byte> nameBytes, string? suffix)
    {
        // Writes a metric name as a double-quoted string ("name<suffix>") with the backslash, quote
        // and line-feed characters escaped. The (legacy) suffix, if any, is written inside the quotes.
        buffer[cursor++] = AsciiQuotationMark;

        foreach (var value in nameBytes)
        {
            switch (value)
            {
                case AsciiQuotationMark:
                    buffer[cursor++] = AsciiReverseSolidus;
                    buffer[cursor++] = AsciiQuotationMark;
                    break;

                case AsciiReverseSolidus:
                    buffer[cursor++] = AsciiReverseSolidus;
                    buffer[cursor++] = AsciiReverseSolidus;
                    break;

                case AsciiLineFeed:
                    buffer[cursor++] = AsciiReverseSolidus;
                    buffer[cursor++] = unchecked((byte)'n');
                    break;

                default:
                    buffer[cursor++] = value;
                    break;
            }
        }

        if (suffix is { Length: > 0 })
        {
            cursor = WriteAsciiStringNoEscape(buffer, cursor, suffix);
        }

        buffer[cursor++] = AsciiQuotationMark;

        return cursor;
    }

    /// <summary>
    /// Writes a label output key, quoting it (as a double-quoted UTF-8 string) when it is not a
    /// valid legacy label name. Only the allow-utf-8 scheme can produce a non-legacy output key; the
    /// underscores, dots and values schemes (and all v0 formats) always produce legacy ASCII names,
    /// which are written verbatim. The quoting therefore needs no knowledge of the negotiated scheme.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="cursor">The current position in the buffer.</param>
    /// <param name="outputKey">The label output key to write.</param>
    /// <returns>The new cursor position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WriteLabelName(byte[] buffer, int cursor, string outputKey)
    {
        if (PrometheusEscaping.IsValidLegacyLabelName(outputKey))
        {
            return WriteAsciiStringNoEscape(buffer, cursor, outputKey);
        }

        buffer[cursor++] = AsciiQuotationMark;
        cursor = WriteLabelValue(buffer, cursor, outputKey);
        buffer[cursor++] = AsciiQuotationMark;

        return cursor;
    }

    internal static int WriteQuotedMetadataName(byte[] buffer, int cursor, ReadOnlySpan<byte> nameBytes)
    {
        // Writes a metadata-line (# TYPE/# HELP/# UNIT) metric family name in the quoted exposition
        // form, e.g. '# TYPE "my.metric" gauge'. Unlike a sample line the name is not wrapped in
        // braces. Shared by the v1.0.0 serializers; the base flow never emits the quoted form.
        cursor = WriteQuotedName(buffer, cursor, nameBytes, suffix: null);

        return cursor;
    }

    internal static int WriteQuotedBucketName(byte[] buffer, int cursor, ReadOnlySpan<byte> nameBytes)
    {
        // Writes a histogram '_bucket' series name in the quoted exposition form and opens the label
        // set ('{"name_bucket",'), leaving the cursor positioned for the first tag.
        buffer[cursor++] = unchecked((byte)'{');
        cursor = WriteQuotedName(buffer, cursor, nameBytes, "_bucket");
        buffer[cursor++] = unchecked((byte)',');

        return cursor;
    }

    internal static int WriteQuotedSeriesNameAndSerializedTags(
        byte[] buffer,
        int cursor,
        ReadOnlySpan<byte> nameBytes,
        string suffix,
        ReadOnlySpan<byte> serializedTags)
    {
        // Writes a histogram '_sum'/'_count' series name and its pre-serialized tags in the quoted
        // exposition form ('{"name<suffix>",tags}').
        buffer[cursor++] = unchecked((byte)'{');
        cursor = WriteQuotedName(buffer, cursor, nameBytes, suffix);

        if (!serializedTags.IsEmpty)
        {
            buffer[cursor++] = unchecked((byte)',');
            cursor = WriteSerializedTagValues(buffer, cursor, serializedTags);
        }

        buffer[cursor++] = unchecked((byte)'}');

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int WriteExemplar(byte[] buffer, int cursor, in Exemplar exemplar, bool isLongValue)
    {
        buffer[cursor++] = unchecked((byte)' ');
        buffer[cursor++] = unchecked((byte)'#');
        buffer[cursor++] = unchecked((byte)' ');

        List<LabelData>? labels = null;

        if (exemplar.TraceId != default)
        {
            this.AddLabel("trace_id", exemplar.TraceId.ToHexString(), ref labels);
        }

        if (exemplar.SpanId != default)
        {
            this.AddLabel("span_id", exemplar.SpanId.ToHexString(), ref labels);
        }

        foreach (var tag in exemplar.FilteredTags)
        {
            this.AddLabel(tag.Key, tag.Value, ref labels, ReservedExemplarLabelNames);
        }

        cursor = WriteLabels(
            buffer,
            cursor,
            labels,
            writeEnclosingBraces: true,
            default,
            null,
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
    internal int WriteTags(
        byte[] buffer,
        int cursor,
        Metric metric,
        ReadOnlyTagCollection tags,
        bool writeEnclosingBraces = true,
        IReadOnlyCollection<string>? reservedOutputKeys = null)
        => this.WriteTags(buffer, cursor, metric, tags, default, null, writeEnclosingBraces, reservedOutputKeys);

    internal int WriteTags(
        byte[] buffer,
        int cursor,
        Metric metric,
        ReadOnlyTagCollection tags,
        ReadOnlySpan<byte> quotedNameBytes,
        string? quotedNameSuffix,
        bool writeEnclosingBraces,
        IReadOnlyCollection<string>? reservedOutputKeys)
    {
        // When quotedNameBytes is non-empty the metric name is embedded as a double-quoted string
        // as the first element inside the braces, e.g. {"my.metric",label="value"}. This is the
        // allow-utf-8 exposition format used when the metric name is not a valid legacy name.
        var startCursor = cursor;
        List<string>? writtenOutputKeys = null;
        var wroteLabel = false;

        if (writeEnclosingBraces)
        {
            buffer[cursor++] = unchecked((byte)'{');
        }

        if (!quotedNameBytes.IsEmpty)
        {
            cursor = WriteQuotedName(buffer, cursor, quotedNameBytes, quotedNameSuffix);
            wroteLabel = true;
        }

        WriteScopeLabels();

        if (TryWritePointTags())
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
            this.AddLabel(tag.Key, tag.Value, ref labels, reservedOutputKeys);
        }

        return WriteLabels(buffer, cursor, labels, writeEnclosingBraces, quotedNameBytes, quotedNameSuffix);

        void WriteScopeLabels()
        {
            // Scope labels (otel_scope_*) are OpenTelemetry naming conventions that are already
            // in their target Prometheus form, so they are not re-escaped by the negotiated
            // scheme. They are de-duplicated by output key in CreateScopeLabels, so unlike point
            // tags they can never collide with an already-written label. They only need to be
            // written (which also records their output keys so point tags can detect a collision
            // with a scope label).
            foreach (var scopeLabel in CreateScopeLabels(metric))
            {
                _ = TryWriteLabel(scopeLabel.Key, scopeLabel.Value, isScopeLabel: true);
            }
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

        bool TryWriteLabel(string key, object? value, bool isScopeLabel = false)
        {
            // Scope labels arrive already in their final (output) form; point tags are escaped
            // using the negotiated scheme. The resulting output key is always a legacy-valid
            // ASCII name, so it is both used for de-duplication and written verbatim.
            var outputKey = isScopeLabel ? key : this.GetOutputLabelKey(key);

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

            cursor = WriteLabelName(buffer, cursor, outputKey);
            cursor = WriteSanitizedLabel(buffer, cursor, value);
            wroteLabel = true;

            return true;
        }
    }

    internal byte[] SerializeTags(
        Metric metric,
        ReadOnlyTagCollection tags,
        IReadOnlyCollection<string>? reservedOutputKeys = null)
    {
        var buffer = new byte[128];

        while (true)
        {
            try
            {
                var cursor = this.WriteTags(
                    buffer,
                    0,
                    metric,
                    tags,
                    writeEnclosingBraces: false,
                    reservedOutputKeys: reservedOutputKeys);

                if (cursor > 0 && buffer[cursor - 1] == unchecked((byte)','))
                {
                    cursor--;
                }

                return buffer.AsSpan(0, cursor).ToArray();
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                buffer = new byte[GetNextSerializedTagsBufferSize(buffer.Length)];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int WriteMetricName(byte[] buffer, int cursor, PrometheusMetric metric)
        => WriteUtf8NoEscape(buffer, cursor, this.GetMetricNameBytes(metric));

    // Writes a metric family name for a metadata (# TYPE/# HELP/# UNIT) line. The base writes the
    // legacy name verbatim; the v1.0.0 formats override this to write a non-legacy allow-utf-8 name
    // as a quoted string, e.g. '# TYPE "my.metric" gauge'.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal virtual int WriteMetricMetadataName(byte[] buffer, int cursor, PrometheusMetric metric)
        => WriteUtf8NoEscape(buffer, cursor, this.GetMetricMetadataNameBytes(metric));

    /// <summary>
    /// Indicates whether the metric name must be written using the quoted exposition format.
    /// </summary>
    /// <param name="metric">The metric to check.</param>
    /// <returns><see langword="true"/> if the metric name requires quoting; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool RequiresQuotedName(PrometheusMetric metric)
        => !metric.GetNameSet(this.Escaping).IsLegacyValid;

    /// <summary>
    /// Writes a metric family name followed by a serialization-time suffix (e.g. "_bucket",
    /// "_sum", "_count", "_created"). For the underscores scheme the pre-computed metadata name
    /// bytes are written verbatim followed by the literal suffix. For the dots and values
    /// schemes the suffix is part of the (unescaped) intended name, so the intended name and
    /// suffix are escaped together as a single unit to keep the structural underscores reversible.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="cursor">The current position in the buffer.</param>
    /// <param name="metric">The metric to write.</param>
    /// <param name="suffix">The suffix to append to the metric name.</param>
    /// <returns>The new cursor position in the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int WriteMetricNameWithSuffix(byte[] buffer, int cursor, PrometheusMetric metric, string suffix)
    {
        // The '_total'/'_bucket'/'_sum'/'_count'/'_created' suffixes are structural suffixes that
        // Prometheus strips to find the metric family, so they are appended literally to the
        // (already escaped) family name regardless of the escaping scheme.
        cursor = this.WriteMetricMetadataName(buffer, cursor, metric);
        return WriteAsciiStringNoEscape(buffer, cursor, suffix);
    }

    internal virtual int WriteSeriesAndTags(
        byte[] buffer,
        int cursor,
        Metric metric,
        PrometheusMetric prometheusMetric,
        ReadOnlyTagCollection tags,
        string? suffix,
        IReadOnlyCollection<string>? reservedOutputKeys)
    {
        // Writes a sample series name (optionally with a structural suffix) followed by its live tags.
        // The base writes the legacy form 'name<suffix>{tags}'; the v1.0.0 formats override this to
        // emit the quoted form '{"name<suffix>",tags}' for a non-legacy allow-utf-8 name.
        cursor = suffix is null
            ? this.WriteMetricName(buffer, cursor, prometheusMetric)
            : this.WriteMetricNameWithSuffix(buffer, cursor, prometheusMetric, suffix);

        return this.WriteTags(buffer, cursor, metric, tags, writeEnclosingBraces: true, reservedOutputKeys: reservedOutputKeys);
    }

    internal int WriteQuotedSeriesAndTags(
        byte[] buffer,
        int cursor,
        Metric metric,
        PrometheusMetric prometheusMetric,
        ReadOnlyTagCollection tags,
        string? suffix,
        IReadOnlyCollection<string>? reservedOutputKeys)
    {
        // Emits a sample series in the quoted exposition form, embedding the (non-legacy) metric name
        // as the first quoted element inside the label braces.

        // Counter and gauge samples embed the full name (with any '_total' baked in); a suffixed
        // series (e.g. '_created') embeds the metadata family name plus the literal suffix.
        var nameBytes = suffix is null
            ? this.GetMetricNameBytes(prometheusMetric)
            : this.GetMetricMetadataNameBytes(prometheusMetric);

        return this.WriteTags(
            buffer,
            cursor,
            metric,
            tags,
            nameBytes,
            suffix,
            writeEnclosingBraces: true,
            reservedOutputKeys: reservedOutputKeys);
    }

    internal virtual int WriteHistogramBucketName(byte[] buffer, int cursor, PrometheusMetric metric)
    {
        // Writes a histogram '_bucket' series name and opens the label set, leaving the cursor
        // positioned for the first tag. The base writes the legacy form 'name_bucket{'; the v1.0.0
        // formats override this to emit the quoted form '{"name_bucket",' for a non-legacy name.
        cursor = this.WriteMetricNameWithSuffix(buffer, cursor, metric, "_bucket");
        buffer[cursor++] = unchecked((byte)'{');

        return cursor;
    }

    // Writes a histogram '_sum'/'_count' series name followed by its (pre-serialized) tags. The
    // base writes the legacy form 'name<suffix>{tags}'; the v1.0.0 formats override this to emit
    // the quoted form '{"name<suffix>",tags}' for a non-legacy allow-utf-8 name.
    internal virtual int WriteSeriesNameAndSerializedTags(
        byte[] buffer,
        int cursor,
        PrometheusMetric metric,
        string suffix,
        ReadOnlySpan<byte> serializedTags)
    {
        cursor = this.WriteMetricNameWithSuffix(buffer, cursor, metric, suffix);
        return WriteSerializedTags(buffer, cursor, serializedTags);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int WriteHelpMetadata(byte[] buffer, int cursor, PrometheusMetric metric, string metricDescription)
    {
        if (string.IsNullOrEmpty(metricDescription))
        {
            return cursor;
        }

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# HELP ");
        cursor = this.WriteMetricMetadataName(buffer, cursor, metric);

        if (!string.IsNullOrEmpty(metricDescription))
        {
            buffer[cursor++] = unchecked((byte)' ');
            cursor = WriteUnicodeString(buffer, cursor, metricDescription);
        }

        buffer[cursor++] = AsciiLineFeed;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int WriteTypeMetadata(byte[] buffer, int cursor, PrometheusMetric metric)
    {
        var metricType = this.MapMetricType(metric.Type);

        Debug.Assert(!string.IsNullOrEmpty(metricType), $"{nameof(metricType)} should not be null or empty.");

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# TYPE ");
        cursor = this.WriteMetricMetadataName(buffer, cursor, metric);
        buffer[cursor++] = unchecked((byte)' ');
        cursor = WriteAsciiStringNoEscape(buffer, cursor, metricType);

        buffer[cursor++] = AsciiLineFeed;

        return cursor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int WriteUnitMetadata(byte[] buffer, int cursor, PrometheusMetric metric, string? unit)
    {
        if (string.IsNullOrEmpty(unit))
        {
            return cursor;
        }

        cursor = WriteAsciiStringNoEscape(buffer, cursor, "# UNIT ");
        cursor = this.WriteMetricMetadataName(buffer, cursor, metric);

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

        buffer[cursor++] = AsciiLineFeed;

        return cursor;
    }

    internal string MapMetricType(PrometheusType type) => type switch
    {
        PrometheusType.Gauge => "gauge",
        PrometheusType.Counter => "counter",
        PrometheusType.Summary => "summary",
        PrometheusType.Histogram => "histogram",
        PrometheusType.Untyped or _ => this.UnknownMetricTypeName,
    };

    /// <summary>
    /// Gets the bytes used when writing a metric's sample family name.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <returns>The bytes representing the metric's sample family name.</returns>
    protected abstract ReadOnlySpan<byte> GetMetricNameBytes(PrometheusMetric metric);

    /// <summary>
    /// Gets the bytes used when writing a metric's metadata (<c>TYPE</c>/<c>UNIT</c>/<c>HELP</c>) name.
    /// </summary>
    /// <param name="metric">The metric.</param>
    /// <returns>The bytes representing the metric's metadata name.</returns>
    protected abstract ReadOnlySpan<byte> GetMetricMetadataNameBytes(PrometheusMetric metric);

    /// <summary>
    /// Writes the value of a histogram bucket's <c>le</c> upper bound.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="cursor">The current position in the buffer.</param>
    /// <param name="explicitBound">The explicit upper bound value.</param>
    /// <returns>The new cursor position after writing.</returns>
    protected abstract int WriteExplicitBound(byte[] buffer, int cursor, double explicitBound);

    /// <summary>
    /// Writes the exemplar (if any) that follows a counter or gauge sample value.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="cursor">The current position in the buffer.</param>
    /// <param name="metricPoint">The metric point.</param>
    /// <param name="prometheusMetric">The Prometheus metric.</param>
    /// <param name="isLongValue">Indicates whether the value is a long.</param>
    /// <returns>The new cursor position after writing.</returns>
    protected abstract int WriteCounterExemplar(byte[] buffer, int cursor, in MetricPoint metricPoint, PrometheusMetric prometheusMetric, bool isLongValue);

    /// <summary>
    /// Writes the <c>_created</c> series (if any) that follows a counter sample.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="cursor">The current position in the buffer.</param>
    /// <param name="metric">The metric.</param>
    /// <param name="prometheusMetric">The Prometheus metric.</param>
    /// <param name="metricPoint">The metric point.</param>
    /// <returns>The new cursor position after writing.</returns>
    protected abstract int WriteCounterCreated(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, in MetricPoint metricPoint);

    /// <summary>
    /// Writes the exemplar (if any) that follows a histogram bucket sample value.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="cursor">The current position in the buffer.</param>
    /// <param name="metricPoint">The metric point.</param>
    /// <param name="lowerBoundExclusive">The exclusive lower bound of the histogram bucket.</param>
    /// <param name="upperBoundInclusive">The inclusive upper bound of the histogram bucket.</param>
    /// <returns>The new cursor position after writing.</returns>
    protected abstract int WriteHistogramBucketExemplar(byte[] buffer, int cursor, in MetricPoint metricPoint, double lowerBoundExclusive, double upperBoundInclusive);

    /// <summary>
    /// Determines whether the histogram <c>_sum</c> and <c>_count</c> series should be written.
    /// </summary>
    /// <param name="hasNegativeBucketBounds">Indicates whether the histogram has negative bucket bounds.</param>
    /// <returns>
    /// <see langword="true"/> if the <c>_sum</c> and <c>_count</c> series should be written; otherwise, <see langword="false"/>.
    /// </returns>
    protected abstract bool ShouldWriteSumAndCount(bool hasNegativeBucketBounds);

    /// <summary>
    /// Writes the <c>_created</c> series (if any) that follows a histogram's samples.
    /// </summary>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="cursor">The current position in the buffer.</param>
    /// <param name="metric">The metric.</param>
    /// <param name="prometheusMetric">The Prometheus metric.</param>
    /// <param name="metricPoint">The metric point.</param>
    /// <returns>The new cursor position after writing.</returns>
    protected abstract int WriteHistogramCreated(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, in MetricPoint metricPoint);

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
        // This is only ever called with an "otel_scope_"-prefixed key, so the value is never
        // empty and never starts with a digit; those cases do not need to be handled here.
        var builder = new StringBuilder(value.Length + 1);
        var lastCharUnderscore = false;

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
                    buffer[cursor++] = AsciiReverseSolidus;
                    buffer[cursor++] = AsciiQuotationMark;
                    value = value[1..];
                    break;
                case '\\':
                    buffer[cursor++] = AsciiReverseSolidus;
                    buffer[cursor++] = AsciiReverseSolidus;
                    value = value[1..];
                    break;
                case '\n':
                    buffer[cursor++] = AsciiReverseSolidus;
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
                case AsciiQuotationMark when escapeQuotationMarks:
                    buffer[cursor++] = AsciiReverseSolidus;
                    buffer[cursor++] = AsciiQuotationMark;
                    break;
                case AsciiReverseSolidus:
                    buffer[cursor++] = AsciiReverseSolidus;
                    buffer[cursor++] = AsciiReverseSolidus;
                    break;
                case AsciiLineFeed:
                    buffer[cursor++] = AsciiReverseSolidus;
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

#if NET
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
#else
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
#endif

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

    private static void AddLabel(
        string originalKey,
        string outputKey,
        object? value,
        ref List<LabelData>? labels,
        IReadOnlyCollection<string>? reservedOutputKeys = null)
    {
        if (reservedOutputKeys?.Contains(outputKey) == true)
        {
            return;
        }

        labels ??= [];
        labels.Add(new LabelData(originalKey, outputKey, GetLabelValueString(value)));
    }

    private static bool IsValidLabelKey(string value)
    {
        if (char.IsAsciiDigit(value[0]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!IsAllowedMetricsLabelCharacter(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string SanitizeLabelKey(string value)
    {
        // The only growth is a single leading underscore added before a leading digit
        var buffer = ArrayPool<char>.Shared.Rent(value.Length + 1);

        try
        {
            var length = 0;
            var lastCharUnderscore = false;

            if (char.IsAsciiDigit(value[0]))
            {
                buffer[length++] = '_';
                lastCharUnderscore = true;
            }

            foreach (var character in value)
            {
                if (!IsAllowedMetricsLabelCharacter(character))
                {
                    if (!lastCharUnderscore)
                    {
                        buffer[length++] = '_';
                        lastCharUnderscore = true;
                    }

                    continue;
                }

                buffer[length++] = character;
                lastCharUnderscore = character == '_';
            }

            return new string(buffer, 0, length);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private static int WriteLabels(
        byte[] buffer,
        int cursor,
        IReadOnlyList<LabelData>? labels,
        bool writeEnclosingBraces,
        ReadOnlySpan<byte> quotedNameBytes,
        string? quotedNameSuffix,
        int? maxLabelSetCharacters = null)
    {
        if (writeEnclosingBraces)
        {
            buffer[cursor++] = unchecked((byte)'{');
        }

        var wroteLabel = false;
        var labelSetCharacters = 0;

        if (!quotedNameBytes.IsEmpty)
        {
            // Embed the metric name as the first (quoted) element inside the braces.
            cursor = WriteQuotedName(buffer, cursor, quotedNameBytes, quotedNameSuffix);
            buffer[cursor++] = unchecked((byte)',');
            wroteLabel = true;
        }

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

#pragma warning disable IDE0370 // Remove unnecessary suppression
            var orderedOutputKeys = orderedKeys!;
            var groupedLabels = labelsBySanitizedKey!;
#pragma warning restore IDE0370 // Remove unnecessary suppression

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

                // The grouped key is already the final output key; it is written verbatim, or
                // quoted when the allow-utf-8 scheme produced a non-legacy label name.
                cursor = WriteLabelName(buffer, cursor, key);
                cursor = WriteSanitizedLabel(buffer, cursor, value);
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

        var symbol = absoluteValue is >= 1e6 or < 1e-4 ? 'e' : 'G';

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

        return value.ToString(absoluteValue is >= 1e6 or < 1e-4 ? "e17" : "G17", CultureInfo.InvariantCulture);

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

    private string GetOutputLabelKey(string value)
    {
        // The dots and values schemes produce a reversible, legacy-valid ASCII label name. The
        // underscores scheme replaces discouraged characters and collapses consecutive ones.
        if (this.Escaping != EscapingScheme.Underscores)
        {
            return string.IsNullOrEmpty(value) ? "_" : PrometheusEscaping.EscapeName(value, this.Escaping);
        }

        if (string.IsNullOrEmpty(value))
        {
            return "_";
        }

        // Check for validity first, since the vast majority of label
        // keys are already valid, to avoid allocating to sanitize it.
        return IsValidLabelKey(value) ? value : SanitizeLabelKey(value);
    }

    private void AddLabel(string originalKey, object? value, ref List<LabelData>? labels, IReadOnlyCollection<string>? reservedOutputKeys = null)
        => AddLabel(originalKey, this.GetOutputLabelKey(originalKey), value, ref labels, reservedOutputKeys);

    private readonly struct LabelData(string originalKey, string outputKey, string value)
    {
        public readonly string OriginalKey { get; } = originalKey;

        public readonly string OutputKey { get; } = outputKey;

        public readonly string Value { get; } = value;
    }
}
