// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Retry;
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

    private readonly OtlpExporterTransmissionHandler<OtlpCollector.ExportTraceServiceRequest, HttpResponseMessage> httpHandler;

    private readonly OtlpExporterTransmissionHandler<OtlpCollector.ExportTraceServiceRequest, OtlpCollector.ExportTraceServiceResponse> grpcHandler;

    private OtlpResource.Resource processResource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the export.</param>
    public OtlpTraceExporter(OtlpExporterOptions options)
        : this(options, new(), null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
    /// </summary>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="grpcHandler">grpc handler.</param>
    /// <param name="httpHandler">http handler.</param>
    internal OtlpTraceExporter(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        OtlpExporterTransmissionHandler<OtlpCollector.ExportTraceServiceRequest, OtlpCollector.ExportTraceServiceResponse> grpcHandler = null,
        OtlpExporterTransmissionHandler<OtlpCollector.ExportTraceServiceRequest, HttpResponseMessage> httpHandler = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");

        this.sdkLimitOptions = sdkLimitOptions;

        OtlpKeyValueTransformer.LogUnsupportedAttributeType = (string tagValueType, string tagKey) =>
        {
            OpenTelemetryProtocolExporterEventSource.Log.UnsupportedAttributeType(tagValueType, tagKey);
        };

        ConfigurationExtensions.LogInvalidEnvironmentVariable = (string key, string value) =>
        {
            OpenTelemetryProtocolExporterEventSource.Log.InvalidEnvironmentVariable(key, value);
        };

        if (exporterOptions.Protocol == OtlpExportProtocol.HttpProtobuf)
        {
            httpHandler = httpHandler ?? exporterOptions.GetHttpTraceHandler(exporterOptions);
        }
        else
        {
            grpcHandler = grpcHandler ?? exporterOptions.GetGrpcTraceHandler(exporterOptions);
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
            request.Return();
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.exportClient?.Shutdown(timeoutMilliseconds) ?? true;
    }
}
