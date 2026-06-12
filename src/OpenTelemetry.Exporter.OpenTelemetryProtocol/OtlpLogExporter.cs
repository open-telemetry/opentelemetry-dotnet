// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Internal;
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

    // POC: process-global counters to assign a stable instance index per
    // component type for otel.component.name. Separate counters per type so
    // each protocol gets a clean "<type>/0..N" sequence.
    private static int grpcInstanceCounter = -1;
    private static int httpInstanceCounter = -1;

    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly ExperimentalOptions experimentalOptions;
    private readonly OtlpExporterTransmissionHandler transmissionHandler;
    private readonly int startWritePosition;
    private readonly string componentType;
    private readonly string componentName;

    // Initial buffer size set to ~732KB.
    // This choice allows us to gradually grow the buffer while targeting a final capacity of around 100 MB,
    // by the 7th doubling to maintain efficient allocation without frequent resizing.
    private byte[] buffer = new byte[750000];

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
        var isGrpc = exporterOptions.Protocol == OtlpExportProtocol.Grpc;
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
        this.startWritePosition = isGrpc ? GrpcStartWritePosition : 0;
        this.transmissionHandler = transmissionHandler ?? exporterOptions.GetExportTransmissionHandler(experimentalOptions, OtlpSignalType.Logs);

        // POC: stable component identity for the otel.sdk.component.shutdown
        // event. Type string comes from the well-known values in the spec
        // registry; instance index is per-type.
        if (isGrpc)
        {
            this.componentType = "otlp_grpc_log_exporter";
            var index = Interlocked.Increment(ref grpcInstanceCounter);
            this.componentName = "otlp_grpc_log_exporter/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            this.componentType = "otlp_http_log_exporter";
            var index = Interlocked.Increment(ref httpInstanceCounter);
            this.componentName = "otlp_http_log_exporter/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
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

        try
        {
            var writePosition = ProtobufOtlpLogSerializer.WriteLogsData(ref this.buffer, this.startWritePosition, this.sdkLimitOptions, this.experimentalOptions, this.Resource, logRecordBatch);

            if (this.startWritePosition == GrpcStartWritePosition)
            {
                // Grpc payload consists of 3 parts
                // byte 0 - Specifying if the payload is compressed.
                // 1-4 byte - Specifies the length of payload in big endian format.
                // 5 and above -  Protobuf serialized data.
                var data = new Span<byte>(this.buffer, 1, 4);
                var dataLength = writePosition - GrpcStartWritePosition;
                BinaryPrimitives.WriteUInt32BigEndian(data, (uint)dataLength);
            }

            if (!this.transmissionHandler.TrySubmitRequest(this.buffer, writePosition))
            {
                return ExportResult.Failure;
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.ExportMethodException(ex);
            return ExportResult.Failure;
        }

        return ExportResult.Success;
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        // POC: time the underlying transmissionHandler shutdown and emit
        // the otel.sdk.component.shutdown event. The handler returns bool,
        // so timed_out is inferred from elapsed >= timeout.
        var startTimestamp = Stopwatch.GetTimestamp();
        var success = this.transmissionHandler?.Shutdown(timeoutMilliseconds) ?? true;
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        var result = SdkSelfObservability.ClassifyResult(success, timeoutMilliseconds, elapsed.TotalMilliseconds);
        SdkSelfObservability.EmitComponentShutdown(
            componentType: this.componentType,
            componentName: this.componentName,
            result: result,
            durationSeconds: elapsed.TotalSeconds);
        return success;
    }
}
