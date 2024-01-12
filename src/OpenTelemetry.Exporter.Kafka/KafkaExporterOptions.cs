// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Kafka;

/// <summary>
/// Kafka exporter options.
/// environment variables are parsed during object construction.
/// </summary>
public class KafkaExporterOptions
{
    internal const string BootstrapServersEnvVarName = "OTEL_EXPORTER_KAFKA_BOOTSTRAP_SERVERS";

    internal const string TimeoutEnvVarName = "OTEL_EXPORTER_KAFKA_TIMEOUT";

    internal const string LogsTopicEnvVarName = "OTEL_EXPORTER_KAFKA_LOGS_TOPIC";

    internal const string MetricsTopicEnvVarName = "OTEL_EXPORTER_KAFKA_METRICS_TOPIC";

    internal const string TraceTopicEnvVarName = "OTEL_EXPORTER_KAFKA_TRACE_TOPIC";

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaExporterOptions"/> class.
    /// </summary>
    public KafkaExporterOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build(), new())
    {
    }

    internal KafkaExporterOptions(
    IConfiguration configuration,
    BatchExportActivityProcessorOptions defaultBatchOptions)
    {
        Debug.Assert(configuration != null, "configuration was null");
        Debug.Assert(defaultBatchOptions != null, "defaultBatchOptions was null");

        if (configuration.TryGetStringValue(BootstrapServersEnvVarName, out var bootstrapServers))
        {
            this.BootstrapServers = bootstrapServers;
        }

        if (configuration.TryGetIntValue(TimeoutEnvVarName, out var timeout))
        {
            this.Timeout = timeout;
        }

        if (configuration.TryGetStringValue(LogsTopicEnvVarName, out var topicLogs))
        {
            this.TopicLogs = topicLogs;
        }

        if (configuration.TryGetStringValue(MetricsTopicEnvVarName, out var topicMetrics))
        {
            this.TopicMetrics = topicMetrics;
        }

        if (configuration.TryGetStringValue(TraceTopicEnvVarName, out var topicTrace))
        {
            this.TopicTrace = topicTrace;
        }

        this.BatchExportProcessorOptions = defaultBatchOptions;
    }

    /// <summary>
    /// Gets or sets BootstrapServers.
    /// e.g. address1:port1[,address2:port2...].
    /// </summary>
    public string BootstrapServers { get; set; }

    /// <summary>
    /// Gets or sets ProducerConfig.
    /// </summary>
    public string ProducerConfig { get; set; }

    /// <summary>
    /// Gets or sets GetTopicTimeout.
    /// in milliseconds.
    /// </summary>
    public int GetTopicTimeout { get; set; }

    /// <summary>
    /// Gets or sets the max waiting time (in milliseconds) for the backend to process each batch.
    /// The default value is 1000.
    /// </summary>
    public int Timeout { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the export processor type to be used with the Kafka Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
    /// </summary>
    /// <remarks>Note: This only applies when exporting traces.</remarks>
    public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

    /// <summary>
    /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is Batch.
    /// </summary>
    /// <remarks>Note: This only applies when exporting traces.</remarks>
    public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; }

    /// <summary>
    /// Gets or sets the Kafka Topic for Logs
    /// The default value is otlp_logs.
    /// </summary>
    public string TopicLogs { get; set; } = "otlp_logs";

    /// <summary>
    /// Gets or sets the Kafka Topic for Metrics
    /// The default value is otlp_metrics.
    /// </summary>
    public string TopicMetrics { get; set; } = "otlp_metrics";

    /// <summary>
    /// Gets or sets the Kafka Topic for Trace
    /// The default value is otlp_spans.
    /// </summary>
    public string TopicTrace { get; set; } = "otlp_spans";

    internal static void RegisterKafkaExporterOptionsFactory(IServiceCollection services)
    {
        services.RegisterOptionsFactory(CreateKafkaExporterOptions);
    }

    internal static KafkaExporterOptions CreateKafkaExporterOptions(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        string name)
        => new(
            configuration,
            serviceProvider.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name));
}
