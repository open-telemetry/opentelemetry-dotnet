// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal sealed class TestExportClient(bool throwException = false) : IExportClient
{
    public bool SendExportRequestCalled { get; private set; }

    public bool ShutdownCalled { get; private set; }

    public bool ThrowException { get; set; } = throwException;

    public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        if (this.ThrowException)
        {
            throw new InvalidOperationException("Exception thrown from SendExportRequest");
        }

        this.SendExportRequestCalled = true;
        return new TestExportClientResponse(true, deadlineUtc, null);
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        this.ShutdownCalled = true;
        return true;
    }

    private sealed class TestExportClientResponse : ExportClientResponse
    {
        public TestExportClientResponse(bool success, DateTime deadline, Exception? exception)
            : base(success, deadline, exception)
        {
        }
    }
}
