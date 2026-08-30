// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Exporter consuming <see cref="LogRecord"/> and exporting the data using
/// the OpenTelemetry protocol (OTLP).
/// </summary>
public sealed class OtlpLogExporter : BaseExporter<LogRecord>
{
    private const int GrpcStartWritePosition = 5;

    // Initial buffer size set to ~732KB, so the buffer can be grown by doubling
    // towards OtlpExporterOptions.MaxRequestSizeBytes without resizing often.
    private const int InitialBufferSize = ProtobufSerializer.InitialBufferSize;

    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly ExperimentalOptions experimentalOptions;
    private readonly OtlpExporterTransmissionHandler transmissionHandler;
    private readonly int startWritePosition;
    private readonly int maxRequestSizeBytes;
    private readonly SerializationBuffer serializationBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the exporter.</param>
    public OtlpLogExporter(OtlpExporterOptions options)
        : this(options ?? throw new ArgumentNullException(nameof(options)), sdkLimitOptions: new(), experimentalOptions: new(), transmissionHandler: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpLogExporter"/> class.
    /// </summary>
    /// <param name="exporterOptions"><see cref="OtlpExporterOptions"/>.</param>
    /// <param name="sdkLimitOptions"><see cref="SdkLimitOptions"/>.</param>
    /// <param name="experimentalOptions"><see cref="ExperimentalOptions"/>.</param>
    /// <param name="transmissionHandler"><see cref="OtlpExporterTransmissionHandler"/>.</param>
    internal OtlpLogExporter(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        OtlpExporterTransmissionHandler? transmissionHandler = null)
    {
        this.experimentalOptions = experimentalOptions;
        this.sdkLimitOptions = sdkLimitOptions;
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        this.startWritePosition = exporterOptions.Protocol == OtlpExportProtocol.Grpc ? GrpcStartWritePosition : 0;
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning

        this.maxRequestSizeBytes = exporterOptions.MaxRequestSizeBytes;
        this.serializationBuffer = new(InitialBufferSize);

        this.transmissionHandler = transmissionHandler ?? exporterOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Logs);
    }

    internal Resource Resource
    {
        get => field ??= this.ParentProvider.GetResource();
        private set;
    }

    /// <inheritdoc/>
#pragma warning disable CA1725 // Parameter names should match base declaration
    public override ExportResult Export(in Batch<LogRecord> logRecordBatch)
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
                writePosition = ProtobufOtlpLogSerializer.WriteLogsData(
                    ref buffer,
                    this.startWritePosition,
                    this.sdkLimitOptions,
                    this.experimentalOptions,
                    this.Resource,
                    logRecordBatch,
                    this.maxRequestSizeBytes + this.startWritePosition);
                serializationSucceeded = true;
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.BatchDroppedDueToSerializationFailure(
                    OtlpSignalType.Logs,
                    logRecordBatch.Count,
                    ex);
                return ExportResult.Failure;
            }

            // The serialization buffer is rented from a pool that can hand back more
            // than was asked for, so the payload can overrun the configured limit.
            // The specification requires that such a request is not made at all.
            var requestSize = writePosition - this.startWritePosition;
            if (requestSize > this.maxRequestSizeBytes)
            {
                OpenTelemetryProtocolExporterEventSource.Log.RequestDiscardedDueToSizeLimit(
                    OtlpSignalType.Logs,
                    logRecordBatch.Count,
                    requestSize,
                    this.maxRequestSizeBytes);

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
            return this.transmissionHandler?.Shutdown(timeoutMilliseconds) ?? true;
        }
        finally
        {
            this.serializationBuffer.Release();
        }
    }
}
