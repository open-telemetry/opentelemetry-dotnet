// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
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
    private readonly OtlpExporterTransmissionHandler<OtlpCollector.ExportLogsServiceRequest> transmissionHandler;
    private readonly OtlpLogRecordTransformer otlpLogRecordTransformer;
    private readonly ILogger<OtlpLogExporter> openTelemetryEventLogger;

    private OtlpResource.Resource? processResource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the exporter.</param>
    public OtlpLogExporter(OtlpExporterOptions options)
        : this(new NullLogger<OtlpLogExporter>(), options, sdkLimitOptions: new(), experimentalOptions: new(), transmissionHandler: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
    /// </summary>
    /// <param name="openTelemetryEventLogger">OpenTelemetryEventLogger for logging internal events.</param>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>.</param>
    /// <param name="transmissionHandler"><see cref="OtlpExporterTransmissionHandler{T}"/>.</param>
    internal OtlpLogExporter(
        ILogger<OtlpLogExporter> openTelemetryEventLogger,
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        OtlpExporterTransmissionHandler<OtlpCollector.ExportLogsServiceRequest>? transmissionHandler = null)
    {
        Debug.Assert(openTelemetryEventLogger != null, "openTelemetryEventLogger was null");
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

        this.openTelemetryEventLogger = openTelemetryEventLogger!;

        this.openTelemetryEventLogger.LogDebug("Hello from Otlp ctor");

        this.transmissionHandler = transmissionHandler ?? exporterOptions!.GetLogsExportTransmissionHandler(experimentalOptions!);

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

        this.openTelemetryEventLogger.LogDebug("Inside export");

        try
        {
            request = this.otlpLogRecordTransformer.BuildExportRequest(this.ProcessResource, logRecordBatch);

            if (!this.transmissionHandler.TrySubmitRequest(request))
            {
                return ExportResult.Failure;
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);

            OpenTelemetryProtocolExporterEvents.ExportMethodException(this.openTelemetryEventLogger, ex);

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
        return this.transmissionHandler?.Shutdown(timeoutMilliseconds) ?? true;
    }
}
