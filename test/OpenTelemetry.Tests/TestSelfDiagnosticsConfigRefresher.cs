// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests;

internal sealed class TestSelfDiagnosticsConfigRefresher(Stream? stream = null) : SelfDiagnosticsConfigRefresher
{
    private readonly Stream? stream = stream;
    private readonly int owningThreadId = Environment.CurrentManagedThreadId;

    public bool TryGetLogStreamCalled { get; private set; }

    public override bool TryGetLogStream(int byteCount, [NotNullWhen(true)] out Stream? stream, out int availableByteCount)
    {
        if (Environment.CurrentManagedThreadId != this.owningThreadId)
        {
            stream = null;
            availableByteCount = 0;
            return false;
        }

        this.TryGetLogStreamCalled = true;
        stream = this.stream;
        availableByteCount = 0;
        return this.stream != null;
    }
}
