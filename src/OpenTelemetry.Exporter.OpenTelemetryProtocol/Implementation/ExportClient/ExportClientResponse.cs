// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal abstract class ExportClientResponse
{
    protected ExportClientResponse(bool success, Exception? exception)
    {
        this.Success = success;
        this.Exception = exception;
    }

    public bool Success { get; }

    public Exception? Exception { get; }
}
