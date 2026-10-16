// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests;

internal sealed class TestSelfDiagnosticsConfigRefresher(Stream? stream = null) : SelfDiagnosticsConfigRefresher
{
    private readonly Stream? stream = stream;
    private readonly int owningThreadId = Environment.CurrentManagedThreadId;

    public bool WriteLogCalled { get; private set; }

    public override void WriteLog(byte[] buffer, int byteCount)
    {
        if (Environment.CurrentManagedThreadId != this.owningThreadId)
        {
            return;
        }

        this.WriteLogCalled = true;
        this.stream?.Write(buffer, 0, byteCount);
    }
}
