// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using Google.Protobuf;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.Kafka.Implementation.ExportClient;

/// <summary>Class for sending trace export request over Kafka.</summary>
internal sealed class KafkaTraceExportClient : BaseKafkaExportClient<OtlpCollector.ExportTraceServiceRequest>
{
    public KafkaTraceExportClient(KafkaExporterOptions options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public override bool SendExportRequest(OtlpCollector.ExportTraceServiceRequest request, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(this.Timeout);

        byte[] byteArray = request.ToByteArray();

        this.producer.ProduceAsync(this.options.TopicTrace, new Message<string, byte[]> { Key = null, Value = byteArray });

        return true;
    }
}
