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

namespace OpenTelemetry.Exporter;

/// <summary>
/// Exporter consuming <see cref="LogRecord"/> and exporting the data using
/// the OpenTelemetry protocol (OTLP).
/// </summary>
public sealed class OtlpLogExporter : BaseExporter<LogRecord>
{
    private readonly IExportClient<OtlpCollector.ExportLogsServiceRequest, OtlpCollector.ExportLogsServiceResponse> exportClient;
    private readonly OtlpLogRecordTransformer otlpLogRecordTransformer;

    private OtlpResource.Resource? processResource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the exporter.</param>
    public OtlpLogExporter(OtlpExporterOptions options)
        : this(options, sdkLimitOptions: new(), experimentalOptions: new(), exportClient: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
    /// </summary>
    /// <param name="exporterOptions">Configuration options for the exporter.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>.</param>
    /// <param name="exportClient">Client used for sending export request.</param>
    internal OtlpLogExporter(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        IExportClient<OtlpCollector.ExportLogsServiceRequest, OtlpCollector.ExportLogsServiceResponse>? exportClient = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

        // Each of the Otlp exporters: Traces, Metrics, and Logs set the same value for `OtlpKeyValueTransformer.LogUnsupportedAttributeType`
        // and `ConfigurationExtensions.LogInvalidEnvironmentVariable` so it should be fine even if these exporters are used together.
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
            this.exportClient = exporterOptions!.GetLogExportClient();
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

            if (!this.exportClient.SendExportRequest(request, out _))
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
