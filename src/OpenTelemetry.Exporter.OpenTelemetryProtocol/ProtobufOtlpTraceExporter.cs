// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Exporter consuming <see cref="Activity"/> and exporting the data using
/// the OpenTelemetry protocol (OTLP).
/// </summary>
internal sealed class ProtobufOtlpTraceExporter : BaseExporter<Activity>
{
    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly ProtobufOtlpExporterTransmissionHandler transmissionHandler;
    private readonly int startWritePosition;

    private Resource? resource;

    // Initial buffer size set to ~732KB.
    // This choice allows us to gradually grow the buffer while targeting a final capacity of around 100 MB,
    // by the 7th doubling to maintain efficient allocation without frequent resizing.
    private byte[] buffer = new byte[750000];

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufOtlpTraceExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the export.</param>
    public ProtobufOtlpTraceExporter(OtlpExporterOptions options)
        : this(options, sdkLimitOptions: new(), experimentalOptions: new(), transmissionHandler: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtobufOtlpTraceExporter"/> class.
    /// </summary>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>.</param>
    /// <param name="transmissionHandler"><see cref="OtlpExporterTransmissionHandler{T}"/>.</param>
    internal ProtobufOtlpTraceExporter(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        ProtobufOtlpExporterTransmissionHandler? transmissionHandler = null)
    {
        Debug.Assert(exporterOptions != null, "exporterOptions was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");

        this.sdkLimitOptions = sdkLimitOptions!;
        this.startWritePosition = exporterOptions!.Protocol == OtlpExportProtocol.Grpc ? 5 : 0;
        this.transmissionHandler = transmissionHandler ?? exporterOptions!.GetProtobufExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces);
    }

    internal Resource Resource => this.resource ??= this.ParentProvider.GetResource();

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Activity> activityBatch)
    {
        // Prevents the exporter's gRPC and HTTP operations from being instrumented.
        using var scope = SuppressInstrumentationScope.Begin();
        return this.TryExport(in activityBatch);
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds) => this.transmissionHandler.Shutdown(timeoutMilliseconds);

    private ExportResult TryExport(in Batch<Activity> activityBatch)
    {
        try
        {
            int writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(this.buffer, this.startWritePosition, this.sdkLimitOptions, this.Resource, activityBatch);

            if (this.startWritePosition == 5)
            {
                // Grpc payload consists of 3 parts
                // byte 0 - Specifying if the payload is compressed.
                // 1-4 byte - Specifies the length of payload in big endian format.
                // 5 and above -  Protobuf serialized data.
                Span<byte> data = new Span<byte>(this.buffer, 1, 4);
                var dataLength = writePosition - 5;
                BinaryPrimitives.WriteUInt32BigEndian(data, (uint)dataLength);
            }

            if (!this.transmissionHandler.TrySubmitRequest(this.buffer, writePosition))
            {
                return ExportResult.Failure;
            }
        }
        catch (IndexOutOfRangeException)
        {
            // Attempt to increase the buffer size
            if (!ProtobufSerializer.IncreaseBufferSize(ref this.buffer, OtlpSignalType.Traces))
            {
                return ExportResult.Failure;
            }

            // Retry serialization after increasing the buffer size.
            // The recursion depth is limited to a maximum of 7 calls, as the buffer size starts at ~732 KB
            // and doubles until it reaches the maximum size of 100 MB. This ensures the recursion remains safe
            // and avoids stack overflow.
            return this.TryExport(activityBatch);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return ExportResult.Failure;
        }

        return ExportResult.Success;
    }
}
