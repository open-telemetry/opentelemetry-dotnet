// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal abstract class ExportClientResponse
{
    protected ExportClientResponse(bool success, DateTime deadline, Exception? exception)
    {
        this.Success = success;
        this.Exception = exception;
        this.Deadline = deadline;
    }

    public bool Success { get; }

    public Exception? Exception { get; }

    public DateTime? Deadline { get; }
}
