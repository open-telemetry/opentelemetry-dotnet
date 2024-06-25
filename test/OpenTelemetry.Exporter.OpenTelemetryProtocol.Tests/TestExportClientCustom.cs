// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Custom.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal class TestExportClientCustom(bool throwException = false) : IExportClient
{
    public bool SendExportRequestCalled { get; private set; }

    public bool ShutdownCalled { get; private set; }

    public bool ThrowException { get; set; } = throwException;

    public HttpRequestMessage CreateHttpRequest(byte[] request, int contentLength)
    {
        throw new NotImplementedException();
    }

    public ExportClientResponse SendExportRequest(byte[] request, int contentLenght, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        if (this.ThrowException)
        {
            throw new Exception("Exception thrown from SendExportRequest");
        }

        this.SendExportRequestCalled = true;
        return new TestExportClientResponse(true, deadlineUtc, null);
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
