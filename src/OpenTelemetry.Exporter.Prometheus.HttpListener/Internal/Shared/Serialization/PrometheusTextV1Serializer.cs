// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

internal sealed class PrometheusTextV1Serializer : PrometheusTextSerializer
{
    internal override int WriteMetricMetadataName(byte[] buffer, int cursor, PrometheusMetric metric) =>
        this.RequiresQuotedName(metric)
            ? WriteQuotedMetadataName(buffer, cursor, this.GetMetricMetadataNameBytes(metric))
            : base.WriteMetricMetadataName(buffer, cursor, metric);

    internal override int WriteSeriesAndTags(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, ReadOnlyTagCollection tags, string? suffix, IReadOnlyCollection<string>? reservedOutputKeys) =>
        this.RequiresQuotedName(prometheusMetric)
            ? this.WriteQuotedSeriesAndTags(buffer, cursor, metric, prometheusMetric, tags, suffix, reservedOutputKeys)
            : base.WriteSeriesAndTags(buffer, cursor, metric, prometheusMetric, tags, suffix, reservedOutputKeys);

    internal override int WriteHistogramBucketName(byte[] buffer, int cursor, PrometheusMetric metric) =>
        this.RequiresQuotedName(metric)
            ? WriteQuotedBucketName(buffer, cursor, this.GetMetricMetadataNameBytes(metric))
            : base.WriteHistogramBucketName(buffer, cursor, metric);

    internal override int WriteSeriesNameAndSerializedTags(byte[] buffer, int cursor, PrometheusMetric metric, string suffix, ReadOnlySpan<byte> serializedTags) =>
        this.RequiresQuotedName(metric)
            ? WriteQuotedSeriesNameAndSerializedTags(buffer, cursor, this.GetMetricMetadataNameBytes(metric), suffix, serializedTags)
            : base.WriteSeriesNameAndSerializedTags(buffer, cursor, metric, suffix, serializedTags);
}
