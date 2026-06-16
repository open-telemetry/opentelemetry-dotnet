// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.Prometheus.Serialization;

internal abstract class TextFormatSerializer
{
    public static OpenMetricsV0Serializer OpenMetricsV0 => field ??= new();

    public static OpenMetricsV1Serializer OpenMetricsV1 => field ??= new();

    public static PrometheusTextV0Serializer PrometheusV0 => field ??= new();

    public static PrometheusTextV1Serializer PrometheusV1 => field ??= new();

    public static TextFormatSerializer GetSerializer(PrometheusProtocol protocol) => protocol switch
    {
        { IsOpenMetrics: true } => protocol.Version.Major switch
        {
            0 => OpenMetricsV0,
            1 => OpenMetricsV1,
            _ => throw new NotSupportedException($"Unsupported OpenMetrics version: {protocol.Version}."),
        },
        { IsOpenMetrics: false } => protocol.Version.Major switch
        {
            0 => PrometheusV0,
            1 => PrometheusV1,
            _ => throw new NotSupportedException($"Unsupported Prometheus version: {protocol.Version}."),
        },
    };

    public bool CanWriteMetric(Metric metric) => throw new NotImplementedException();

    public string CreateScopeIdentity(Metric metric) => throw new NotImplementedException();

    public int WriteEof(byte[] buffer, int cursor) => throw new NotImplementedException();

    public int WriteMetric(
        byte[] buffer,
        int cursor,
        Metric metric,
        PrometheusMetric prometheusMetric,
        bool writeType,
        bool writeUnit,
        bool writeHelp,
        string? unitOverride,
        string? helpOverride) => throw new NotImplementedException();

    public int WriteScopeInfo(byte[] buffer, int cursor, string scopeName) => throw new NotImplementedException();

    public int WriteScopeInfoMetadata(byte[] buffer, int cursor) => throw new NotImplementedException();

    public int WriteTargetInfo(byte[] buffer, int cursor, Resource resource) => throw new NotImplementedException();
}
