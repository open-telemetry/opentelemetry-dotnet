// <copyright file="Http2UnencryptedSupportTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
