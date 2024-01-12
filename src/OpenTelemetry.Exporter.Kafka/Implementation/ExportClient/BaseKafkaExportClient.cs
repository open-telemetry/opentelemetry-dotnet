// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Confluent.Kafka;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Kafka.Implementation.ExportClient;

/// <summary>Base class for sending export request over Kafka.</summary>
/// <typeparam name="TRequest">Type of export request.</typeparam>
internal abstract class BaseKafkaExportClient<TRequest> : IExportClient<TRequest>
{
    protected readonly KafkaExporterOptions options;

    protected readonly ProducerConfig producerConfig;

    protected readonly ProducerBuilder<string, byte[]> producerBuilder;

    protected readonly IProducer<string, byte[]> producer;

    protected BaseKafkaExportClient(KafkaExporterOptions options)
    {
        Guard.ThrowIfNull(options);

        Guard.ThrowIfInvalidTimeout(options.Timeout);

        this.options = options;

        this.Timeout = options.Timeout;

        this.producerConfig = new ProducerConfig();

        this.producerConfig.BootstrapServers = options.BootstrapServers;

        this.producerBuilder = new ProducerBuilder<string, byte[]>(this.producerConfig);

        this.producer = this.producerBuilder.Build();
    }

    internal int Timeout { get; }

    /// <inheritdoc/>
    public abstract bool SendExportRequest(TRequest request, CancellationToken cancellationToken = default);

    /// <inheritdoc/>
    public virtual bool Shutdown(int timeoutMilliseconds)
    {
        if (this.producer == null)
        {
            return true;
        }

        this.producer.Dispose();

        // TODO
        return true;
    }
}
