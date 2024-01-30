// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP trace export request over gRPC.</summary>
internal sealed class OtlpGrpcTraceExportClient : BaseOtlpGrpcExportClient<OtlpCollector.ExportTraceServiceRequest, OtlpCollector.ExportTraceServiceResponse>
{
    private readonly OtlpCollector.TraceService.TraceServiceClient traceClient;

    public OtlpGrpcTraceExportClient(OtlpExporterOptions options, OtlpCollector.TraceService.TraceServiceClient traceServiceClient = null)
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
    public override bool SendExportRequest(OtlpCollector.ExportTraceServiceRequest request, out OtlpCollector.ExportTraceServiceResponse response, CancellationToken cancellationToken = default)
    {
        response = null;
        var deadline = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);

        try
        {
            response = this.traceClient.Export(request, headers: this.Headers, deadline: deadline, cancellationToken: cancellationToken);
        }
        catch (RpcException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

            return false;
        }

        return true;
    }
}
