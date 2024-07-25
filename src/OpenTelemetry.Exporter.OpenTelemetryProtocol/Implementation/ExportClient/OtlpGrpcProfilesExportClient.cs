// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Grpc.Core;
using OtlpCollector = OpenTelemetry.Proto.Collector.Profiles.V1Experimental;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Class for sending OTLP Logs export request over gRPC.</summary>
internal sealed class OtlpGrpcProfilesExportClient : BaseOtlpGrpcExportClient<OtlpCollector.ExportProfilesServiceRequest>
{
    private readonly OtlpCollector.ProfilesService.ProfilesServiceClient profilesClient;

    public OtlpGrpcProfilesExportClient(OtlpExporterOptions options, OtlpCollector.ProfilesService.ProfilesServiceClient? profilesServiceClient = null)
        : base(options)
    {
        if (profilesServiceClient != null)
        {
            this.profilesClient = profilesServiceClient;
        }
        else
        {
            this.Channel = options.CreateChannel();
            this.profilesClient = new OtlpCollector.ProfilesService.ProfilesServiceClient(this.Channel);
        }
    }

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(OtlpCollector.ExportProfilesServiceRequest request, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            this.profilesClient.Export(request, headers: this.Headers, deadline: deadlineUtc, cancellationToken: cancellationToken);

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
