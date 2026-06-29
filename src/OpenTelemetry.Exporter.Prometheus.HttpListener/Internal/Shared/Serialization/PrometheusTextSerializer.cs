// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

/// <summary>
/// Serializes metrics using the Prometheus exposition text format.
/// </summary>
/// <remarks>
/// The Prometheus text format does not emit exemplars or "_created" series, and writes
/// histogram bucket bounds and "_sum"/"_count" series unconditionally.
/// </remarks>
internal abstract class PrometheusTextSerializer : TextFormatSerializer
{
    protected override string UnknownMetricTypeName => "untyped";

    protected override string TargetInfoTypeName => "target_info";

    protected override string TargetInfoTypeValue => "gauge";

    public override string GetMetadataName(PrometheusMetric metric)
        => metric.Name;

    protected override ReadOnlySpan<byte> GetMetricNameBytes(PrometheusMetric metric)
        => metric.NameBytes;

    protected override ReadOnlySpan<byte> GetMetricMetadataNameBytes(PrometheusMetric metric)
        => metric.NameBytes;

    protected override int WriteExplicitBound(byte[] buffer, int cursor, double explicitBound)
        => WriteDouble(buffer, cursor, explicitBound);

    protected override bool ShouldWriteSumAndCount(bool hasNegativeBucketBounds)
        => true;

    protected override int WriteCounterExemplar(byte[] buffer, int cursor, in MetricPoint metricPoint, PrometheusMetric prometheusMetric, bool isLongValue)
        => cursor;

    protected override int WriteCounterCreated(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, in MetricPoint metricPoint, in TextFormatSerializerOptions options)
        => cursor;

    protected override int WriteHistogramBucketExemplar(byte[] buffer, int cursor, in MetricPoint metricPoint, double lowerBoundExclusive, double upperBoundInclusive)
        => cursor;

    protected override int WriteHistogramCreated(byte[] buffer, int cursor, Metric metric, PrometheusMetric prometheusMetric, in MetricPoint metricPoint, in TextFormatSerializerOptions options)
        => cursor;
}
