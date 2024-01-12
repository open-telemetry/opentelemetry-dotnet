// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.Kafka;

/// <summary>
/// Exporter consuming <see cref="LogRecord"/> and exporting the data to Kafka.
/// </summary>
public class KafkaLogsExporter : BaseExporter<LogRecord>
{
    private readonly SdkLimitOptions? sdkLimitOptions;

    private readonly ExperimentalOptions? experimentalOptions;

    private readonly IExportClient<OtlpCollector.ExportLogsServiceRequest> exportClient;

    private readonly OtlpLogRecordTransformer otlpLogRecordTransformer;

    private OtlpResource.Resource? processResource;

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaLogsExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the exporter.</param>
    public KafkaLogsExporter(KafkaExporterOptions options)
        : this(options, sdkLimitOptions: new(), experimentalOptions: new(), exportClient: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KafkaLogsExporter"/> class.
    /// </summary>
    /// <param name="exporterOptions"><see cref="KafkaExporterOptions"/>.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>.</param>
    /// <param name="exportClient">Client used for sending export request.</param>
    internal KafkaLogsExporter(
        KafkaExporterOptions exporterOptions,
        SdkLimitOptions? sdkLimitOptions,
        ExperimentalOptions? experimentalOptions,
        IExportClient<OtlpCollector.ExportLogsServiceRequest>? exportClient = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");
        this.sdkLimitOptions = sdkLimitOptions;
        this.experimentalOptions = experimentalOptions;
        OtlpKeyValueTransformer.LogUnsupportedAttributeType = (string tagValueType, string tagKey) =>
        {
            OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(tagValueType, tagKey);
        };
        ConfigurationExtensions.LogInvalidEnvironmentVariable = (string key, string value) =>
        {
            OpenTelemetryProtocolExporterEventSource.Log.InvalidEnvironmentVariable(key, value);
        };
        if (exportClient != null)
        {
            this.exportClient = exportClient;
        }
        else
        {
            this.exportClient = exporterOptions!.GetLogsExportClient();
        }

        this.otlpLogRecordTransformer = new OtlpLogRecordTransformer(sdkLimitOptions!, experimentalOptions!);
    }

    internal OtlpResource.Resource ProcessResource
         => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<LogRecord> logRecordBatch)
    {
        // Prevents the exporter's gRPC and HTTP operations from being instrumented.
        using var scope = SuppressInstrumentationScope.Begin();
        OtlpCollector.ExportLogsServiceRequest? request = null;
        try
        {
            request = this.otlpLogRecordTransformer.BuildExportRequest(this.ProcessResource, logRecordBatch);
            if (!this.exportClient.SendExportRequest(request))
            {
                return ExportResult.Failure;
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return ExportResult.Failure;
        }
        finally
        {
            if (request != null)
            {
                this.otlpLogRecordTransformer.Return(request);
            }
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.exportClient?.Shutdown(timeoutMilliseconds) ?? true;
    }
}
