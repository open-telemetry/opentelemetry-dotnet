// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using Google.Protobuf;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;

namespace OpenTelemetry.Exporter.Kafka.Implementation.ExportClient;

/// <summary>Class for sending logs export request over Kafka.</summary>
internal sealed class KafkaLogsExportClient : BaseKafkaExportClient<OtlpCollector.ExportLogsServiceRequest>
{
    public KafkaLogsExportClient(KafkaExporterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public override bool SendExportRequest(OtlpCollector.ExportLogsServiceRequest request, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(this.Timeout);

        byte[] byteArray = request.ToByteArray();

        this.producer.ProduceAsync(this.options.TopicLogs, new Message<string, byte[]> { Key = null, Value = byteArray });

        return true;
    }
}
