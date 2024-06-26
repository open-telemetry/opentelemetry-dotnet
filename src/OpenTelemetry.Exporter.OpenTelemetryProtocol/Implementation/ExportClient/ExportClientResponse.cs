// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal abstract class ExportClientResponse
{
    protected ExportClientResponse(bool success, DateTime deadlineUtc, Exception? exception)
    {
        this.Success = success;
        this.Exception = exception;
        this.DeadlineUtc = deadlineUtc;
    }

    [MemberNotNullWhen(false, nameof(Exception))]
    public bool Success { get; }

    public Exception? Exception { get; }

    public DateTime DeadlineUtc { get; }
}
