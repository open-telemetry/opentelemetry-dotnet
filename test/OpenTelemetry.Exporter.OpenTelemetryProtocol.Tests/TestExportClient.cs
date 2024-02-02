// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal class TestExportClient<T>(bool throwException = false) : IExportClient<T>
{
    public bool SendExportRequestCalled { get; private set; }

    public bool ShutdownCalled { get; private set; }

    public bool ThrowException { get; set; } = throwException;

    public ExportClientResponse SendExportRequest(T request, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(1000);
        if (this.ThrowException)
        {
            throw new Exception("Exception thrown from SendExportRequest");
        }

        this.SendExportRequestCalled = true;
        return new TestExportClientResponse(true, deadline, null);
    }

    public bool Shutdown(int timeoutMilliseconds)
    {
        this.ShutdownCalled = true;
        return true;
    }

    private class TestExportClientResponse : ExportClientResponse
    {
        public TestExportClientResponse(bool success, DateTime deadline, Exception exception)
            : base(success, deadline, exception)
        {
        }
    }
}
