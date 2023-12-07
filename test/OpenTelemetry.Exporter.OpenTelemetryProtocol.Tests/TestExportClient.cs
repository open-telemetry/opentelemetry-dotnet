// <copyright file="TestExportClient.cs" company="OpenTelemetry Authors">
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
