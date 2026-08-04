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
public class OtlpTraceExporter : BaseExporter<Activity>
{
    private const int GrpcStartWritePosition = 5;

    // Initial buffer size set to ~732KB, so the buffer can be grown by doubling
    // towards OtlpExporterOptions.MaxExportPayloadSizeBytes without resizing often.
    private const int InitialBufferSize = ProtobufSerializer.InitialBufferSize;

    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly OtlpExporterTransmissionHandler transmissionHandler;
    private readonly int startWritePosition;
    private readonly int maxExportPayloadSizeBytes;
    private readonly SerializationBuffer serializationBuffer = new(InitialBufferSize);

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the export.</param>
    public OtlpTraceExporter(OtlpExporterOptions options)
        : this(options ?? throw new ArgumentNullException(nameof(options)), sdkLimitOptions: new(), experimentalOptions: new(), transmissionHandler: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpTraceExporter"/> class.
    /// </summary>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>.</param>
    /// <param name="transmissionHandler"><see cref="OtlpExporterTransmissionHandler"/>.</param>
    internal OtlpTraceExporter(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        OtlpExporterTransmissionHandler? transmissionHandler = null)
    {
        this.sdkLimitOptions = sdkLimitOptions;
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        this.startWritePosition = exporterOptions.Protocol == OtlpExportProtocol.Grpc ? GrpcStartWritePosition : 0;
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
        this.maxExportPayloadSizeBytes = exporterOptions.MaxExportPayloadSizeBytes;
        this.transmissionHandler = transmissionHandler ?? exporterOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Traces);
    }

    internal Resource Resource
    {
        get => field ??= this.ParentProvider.GetResource();
        private set;
    }

    /// <inheritdoc/>
#pragma warning disable CA1725 // Parameter names should match base declaration
    public override ExportResult Export(in Batch<Activity> activityBatch)
#pragma warning restore CA1725 // Parameter names should match base declaration
    {
        // Prevents the exporter's gRPC and HTTP operations from being instrumented.
        using var scope = SuppressInstrumentationScope.Begin();

        byte[]? buffer = null;
        var serializationSucceeded = false;
        var writePosition = 0;

        try
        {
            buffer = this.serializationBuffer.Rent();

            try
            {
                writePosition = ProtobufOtlpTraceSerializer.WriteTraceData(
                    ref buffer,
                    this.startWritePosition,
                    this.sdkLimitOptions,
                    this.Resource,
                    activityBatch,
                    this.maxExportPayloadSizeBytes);
                serializationSucceeded = true;
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.BatchDroppedDueToSerializationFailure(
                    OtlpSignalType.Traces,
                    activityBatch.Count,
                    ex);
                return ExportResult.Failure;
            }

            // The serialization buffer is rented from a pool that may hand back more
            // than was asked for, so serialization can overrun the configured maximum.
            // Enforce the limit against the payload itself rather than the capacity.
            var payloadSize = writePosition - this.startWritePosition;
            if (payloadSize > this.maxExportPayloadSizeBytes)
            {
                OpenTelemetryProtocolExporterEventSource.Log.BatchDroppedDueToPayloadSizeLimit(
                    OtlpSignalType.Traces,
                    activityBatch.Count,
                    payloadSize,
                    this.maxExportPayloadSizeBytes);

                // Discard the oversized buffer rather than keeping it as the size hint.
                serializationSucceeded = false;
                return ExportResult.Failure;
            }

            if (this.startWritePosition == GrpcStartWritePosition)
            {
                // Grpc payload consists of 3 parts
                // byte 0 - Specifying if the payload is compressed.
                // 1-4 byte - Specifies the length of payload in big endian format.
                // 5 and above -  Protobuf serialized data.
                // Note: byte 0 must be explicitly cleared because the rented buffer is not zeroed.
                buffer[0] = 0;
                var data = new Span<byte>(buffer, 1, 4);
                var dataLength = writePosition - GrpcStartWritePosition;
                BinaryPrimitives.WriteUInt32BigEndian(data, (uint)dataLength);
            }

            if (!this.transmissionHandler.TrySubmitRequest(buffer, writePosition))
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
            if (buffer != null)
            {
                if (serializationSucceeded)
                {
                    this.serializationBuffer.Return(buffer, writePosition);
                }
                else
                {
                    this.serializationBuffer.Discard(buffer);
                }
            }
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        try
        {
            return this.transmissionHandler.Shutdown(timeoutMilliseconds);
        }
        finally
        {
            this.serializationBuffer.Release();
        }
    }
}
