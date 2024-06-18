// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

// using Google.Protobuf;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;
using OpenTelemetry.Resources;

// using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Exporter consuming <see cref="Activity"/> and exporting the data using
/// the OpenTelemetry protocol (OTLP).
/// </summary>
internal class OtlpTraceExporterNew : BaseExporter<Activity>
{
    [ThreadStatic]
    private static byte[] buffer;

    private readonly OtlpExporterTransmissionHandler transmissionHandler;
    private readonly ActivitySerializer activitySerializer;
    private readonly int bufferOffSet;

    private Resource resource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpTraceExporterNew"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the export.</param>
    internal OtlpTraceExporterNew(OtlpExporterOptions options)
        : this(options, sdkLimitOptions: new(), experimentalOptions: new(), transmissionHandler: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpTraceExporterNew"/> class.
    /// </summary>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>.</param>
    /// <param name="transmissionHandler"><see cref="OtlpExporterTransmissionHandler"/>.</param>
    internal OtlpTraceExporterNew(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        OtlpExporterTransmissionHandler transmissionHandler = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions was null");

        this.transmissionHandler = exporterOptions.GetTraceExportTransmissionHandlerNew(experimentalOptions);
        this.activitySerializer = new ActivitySerializer(sdkLimitOptions);

        if (exporterOptions.Protocol == OtlpExportProtocol.Grpc)
        {
            this.bufferOffSet = 5;
        }
    }

    internal Resource Resource => this.resource ??= this.ParentProvider?.GetResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> activityBatch)
    {
        // Prevents the exporter's gRPC and HTTP operations from being instrumented.
        using var scope = SuppressInstrumentationScope.Begin();

        buffer ??= new byte[100000];

        var cursor = this.activitySerializer.Serialize(ref buffer, this.bufferOffSet, this.Resource, activityBatch);

        var arrcopy = new byte[cursor - this.bufferOffSet];
        Buffer.BlockCopy(buffer, this.bufferOffSet, arrcopy, 0, arrcopy.Length);

        // var request = new OtlpCollector.ExportTraceServiceRequest();

        // request.MergeFrom(arrcopy!);

        if (!this.transmissionHandler.TrySubmitRequest(buffer, cursor))
        {
            return ExportResult.Failure;
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.transmissionHandler.Shutdown(timeoutMilliseconds);
    }
}
