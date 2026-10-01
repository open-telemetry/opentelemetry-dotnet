// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

internal sealed class NullScope : IDisposable
{
    private NullScope()
    {
    }

    public static NullScope Instance { get; } = new();

    public void Dispose()
    {
    }
}
