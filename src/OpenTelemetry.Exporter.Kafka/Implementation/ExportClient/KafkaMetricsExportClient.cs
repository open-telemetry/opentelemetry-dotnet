// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using Google.Protobuf;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;

namespace OpenTelemetry.Exporter.Kafka.Implementation.ExportClient;

/// <summary>Class for sending metrics export request over Kafka.</summary>
internal sealed class KafkaMetricsExportClient : BaseKafkaExportClient<OtlpCollector.ExportMetricsServiceRequest>
{
    public KafkaMetricsExportClient(KafkaExporterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public override bool SendExportRequest(OtlpCollector.ExportMetricsServiceRequest request, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(this.Timeout);

        byte[] byteArray = request.ToByteArray();

        this.producer.ProduceAsync(this.options.TopicMetrics, new Message<string, byte[]> { Key = null, Value = byteArray });

        return true;
    }
}
