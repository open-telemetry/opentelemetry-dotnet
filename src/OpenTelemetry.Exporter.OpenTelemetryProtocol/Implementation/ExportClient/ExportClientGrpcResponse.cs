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
        string? grpcStatusDetailsHeader)
        : base(success, deadlineUtc, exception)
    {
        this.Status = status;
        this.GrpcStatusDetailsHeader = grpcStatusDetailsHeader;
    }

    public Status? Status { get; }

    public string? GrpcStatusDetailsHeader { get; }
}
