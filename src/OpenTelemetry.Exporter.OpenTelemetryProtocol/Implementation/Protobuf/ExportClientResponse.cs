// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

internal abstract class ExportClientResponse
{
    protected ExportClientResponse(bool success, DateTime deadlineUtc, Exception? exception)
    {
        this.Success = success;
        this.Exception = exception;
        this.DeadlineUtc = deadlineUtc;
    }

    public bool Success { get; }

    public Exception? Exception { get; }

    public DateTime DeadlineUtc { get; }
}
