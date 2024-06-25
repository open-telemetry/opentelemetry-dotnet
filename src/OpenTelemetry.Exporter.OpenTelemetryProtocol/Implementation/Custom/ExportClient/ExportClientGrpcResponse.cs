// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Custom.ExportClient;

internal sealed class ExportClientGrpcResponse : ExportClientResponse
{
    public ExportClientGrpcResponse(
        bool success,
        DateTime deadlineUtc,
        Exception? exception)
        : base(success, deadlineUtc, exception)
    {
    }
}
