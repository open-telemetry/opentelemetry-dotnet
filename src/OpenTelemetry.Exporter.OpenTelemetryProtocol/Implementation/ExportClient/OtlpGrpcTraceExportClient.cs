// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP trace export request over gRPC.</summary>
internal sealed class OtlpGrpcTraceExportClient : BaseOtlpGrpcExportClient<OtlpCollector.ExportTraceServiceRequest>
{
    private readonly OtlpCollector.TraceService.TraceServiceClient traceClient;

    public OtlpGrpcTraceExportClient(OtlpExporterOptions options, OtlpCollector.TraceService.TraceServiceClient? traceServiceClient = null)
        : base(options)
    {
        if (traceServiceClient != null)
        {
            this.traceClient = traceServiceClient;
        }
        else
        {
            this.Channel = options.CreateChannel();
            this.traceClient = new OtlpCollector.TraceService.TraceServiceClient(this.Channel);
        }
    }

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(OtlpCollector.ExportTraceServiceRequest request, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            this.traceClient.Export(request, headers: this.Headers, deadline: deadlineUtc, cancellationToken: cancellationToken);

            // We do not need to return back response and deadline for successful response so using cached value.
            return SuccessExportResponse;
        }
        catch (RpcException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

            return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: ex);
        }
    }
}
