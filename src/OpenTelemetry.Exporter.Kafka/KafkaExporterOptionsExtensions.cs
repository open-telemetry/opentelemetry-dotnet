// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Kafka.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using LogOtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using MetricsOtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using TraceOtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.Kafka;

internal static class KafkaExporterOptionsExtensions
{
    public static IExportClient<TraceOtlpCollector.ExportTraceServiceRequest> GetTraceExportClient(this KafkaExporterOptions options)
    {
        return new KafkaTraceExportClient(options);
    }

    public static IExportClient<MetricsOtlpCollector.ExportMetricsServiceRequest> GetMetricsExportClient(this KafkaExporterOptions options)
    {
        return new KafkaMetricsExportClient(options);
    }

    public static IExportClient<LogOtlpCollector.ExportLogsServiceRequest> GetLogsExportClient(this KafkaExporterOptions options)
    {
        return new KafkaLogsExportClient(options);
    }
}
