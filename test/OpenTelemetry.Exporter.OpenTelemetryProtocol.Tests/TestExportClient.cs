// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal class TestExportClient<T>(bool throwException = false) : IExportClient<T>
{
    public bool SendExportRequestCalled { get; private set; }

    public bool ShutdownCalled { get; private set; }

    public bool ThrowException { get; set; } = throwException;

    public bool SendExportRequest(T request, CancellationToken cancellationToken = default)
    {
        if (this.ThrowException)
        {
            throw new Exception("Exception thrown from SendExportRequest");
        }

        this.SendExportRequestCalled = true;
        return true;
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        this.ShutdownCalled = true;
        return true;
    }
}
