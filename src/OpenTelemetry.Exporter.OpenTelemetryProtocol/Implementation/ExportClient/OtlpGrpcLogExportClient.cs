// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP Logs export request over gRPC.</summary>
internal sealed class OtlpGrpcLogExportClient : BaseOtlpGrpcExportClient<OtlpCollector.ExportLogsServiceRequest>
{
    private readonly OtlpCollector.LogsService.LogsServiceClient logsClient;

    public OtlpGrpcLogExportClient(OtlpExporterOptions options, OtlpCollector.LogsService.LogsServiceClient logsServiceClient = null)
        : base(options)
    {
        if (logsServiceClient != null)
        {
            this.logsClient = logsServiceClient;
        }
        else
        {
            this.Channel = options.CreateChannel();
            this.logsClient = new OtlpCollector.LogsService.LogsServiceClient(this.Channel);
        }
    }

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(OtlpCollector.ExportLogsServiceRequest request, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);

        try
        {
            this.logsClient.Export(request, headers: this.Headers, deadline: deadline, cancellationToken: cancellationToken);

            // We do not need to return back response and deadline for successful response so using cached value.
            return SuccessExportResponse;
        }
        catch (RpcException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

            return new ExportClientGrpcResponse(success: false, deadlineUtc: deadline, exception: ex);
        }
    }
}
