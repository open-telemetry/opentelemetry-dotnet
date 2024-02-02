// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP metrics export request over gRPC.</summary>
internal sealed class OtlpGrpcMetricsExportClient : BaseOtlpGrpcExportClient<OtlpCollector.ExportMetricsServiceRequest>
{
    private readonly OtlpCollector.MetricsService.MetricsServiceClient metricsClient;

    public OtlpGrpcMetricsExportClient(OtlpExporterOptions options, OtlpCollector.MetricsService.MetricsServiceClient metricsServiceClient = null)
        : base(options)
    {
        if (metricsServiceClient != null)
        {
            this.metricsClient = metricsServiceClient;
        }
        else
        {
            this.Channel = options.CreateChannel();
            this.metricsClient = new OtlpCollector.MetricsService.MetricsServiceClient(this.Channel);
        }
    }

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(OtlpCollector.ExportMetricsServiceRequest request, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(this.TimeoutMilliseconds);

        try
        {
            this.metricsClient.Export(request, headers: this.Headers, deadline: deadline, cancellationToken: cancellationToken);

            return new ExportClientGrpcResponse(success: true, deadline: deadline, exception: null);
        }
        catch (RpcException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

            return new ExportClientGrpcResponse(success: false, deadline: deadline, exception: ex);
        }
    }
}
