// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal sealed class ExportClientGrpcResponse : ExportClientResponse
{
    public ExportClientGrpcResponse(
        bool success,
        DateTime deadlineUtc,
        Exception? exception,
        Status? status,
        string? grpcStatusDetailsHeader,
        string? grpcAcceptEncodingHeader = null)
        : base(success, deadlineUtc, exception)
    {
        this.Status = status;
        this.GrpcStatusDetailsHeader = grpcStatusDetailsHeader;
        this.GrpcAcceptEncodingHeader = grpcAcceptEncodingHeader;
    }

    public Status? Status { get; }

    public string? GrpcStatusDetailsHeader { get; }

    /// <summary>
    /// Gets the value of the <c>grpc-accept-encoding</c> header of the response, if it was captured.
    /// </summary>
    public string? GrpcAcceptEncodingHeader { get; }
}
