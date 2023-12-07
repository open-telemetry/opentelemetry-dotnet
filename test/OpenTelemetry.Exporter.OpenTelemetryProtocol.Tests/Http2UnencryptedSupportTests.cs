// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class Http2UnencryptedSupportTests : IDisposable
{
    private readonly bool initialFlagStatus;

    public Http2UnencryptedSupportTests()
    {
        this.initialFlagStatus = DetermineInitialFlagStatus();
    }

    public void Dispose()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", this.initialFlagStatus);
        GC.SuppressFinalize(this);
    }

    private static bool DetermineInitialFlagStatus()
    {
        if (AppContext.TryGetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", out var flag))
        {
            return flag;
        }

        return false;
    }
}