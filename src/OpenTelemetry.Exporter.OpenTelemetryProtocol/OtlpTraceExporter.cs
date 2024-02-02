// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using Google.Protobuf;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Internal;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Exporter consuming <see cref="Activity"/> and exporting the data using
/// the OpenTelemetry protocol (OTLP).
/// </summary>
public class OtlpTraceExporter : BaseExporter<Activity>
{
    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly OtlpExporterTransmissionHandler<OtlpCollector.ExportTraceServiceRequest> transmissionHandler;

    private OtlpResource.Resource? processResource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the export.</param>
    public OtlpTraceExporter(OtlpExporterOptions options)
        : this(options, sdkLimitOptions: new(), transmissionHandler: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
    /// </summary>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="transmissionHandler"><see cref="OtlpExporterTransmissionHandler{T}"/>.</param>
    internal OtlpTraceExporter(
    OtlpExporterOptions exporterOptions,
    SdkLimitOptions sdkLimitOptions,
    OtlpExporterTransmissionHandler<OtlpCollector.ExportTraceServiceRequest>? transmissionHandler = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");

        this.sdkLimitOptions = sdkLimitOptions!;

        OtlpKeyValueTransformer.LogUnsupportedAttributeType = OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType;

        ConfigurationExtensions.LogInvalidEnvironmentVariable = OpenTelemetryProtocolExporterEventSource.Log.InvalidEnvironmentVariable;

        if (exporterOptions!.RetryStrategy == RetryStrategy.InMemory)
        {
            this.transmissionHandler = new OtlpExporterRetryTransmissionHandler<OtlpCollector.ExportTraceServiceRequest>(exporterOptions.GetTraceExportClient());
        }
        else if (exporterOptions!.RetryStrategy == RetryStrategy.Storage)
        {
            try
            {
                this.transmissionHandler = new OtlpExporterPersistentStorageRetryTransmissionHandler<OtlpCollector.ExportTraceServiceRequest>(
                    exporterOptions.GetTraceExportClient(),
                    requestFactory: (byte[] data) =>
                    {
                        var request = new OtlpCollector.ExportTraceServiceRequest();
                        request.MergeFrom(data);
                        return request;
                    },
                    Path.Combine(exporterOptions.StorageDirectory, "traces"));
            }
            catch
            {
                // TODO: log exception
                this.transmissionHandler = exporterOptions.GetTraceExportTransmissionHandler();
            }
        }
        else
        {
            this.transmissionHandler = transmissionHandler ?? exporterOptions.GetTraceExportTransmissionHandler();
        }
    }

    internal OtlpResource.Resource ProcessResource => this.processResource ??= this.ParentProvider.GetResource().ToOtlpResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> activityBatch)
    {
        // Prevents the exporter's gRPC and HTTP operations from being instrumented.
        using var scope = SuppressInstrumentationScope.Begin();

        var request = new OtlpCollector.ExportTraceServiceRequest();

        try
        {
            request.AddBatch(this.sdkLimitOptions, this.ProcessResource, activityBatch);

            if (!this.transmissionHandler.SubmitRequest(request))
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
            request.Return();
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.transmissionHandler.Shutdown(timeoutMilliseconds);
    }
}
