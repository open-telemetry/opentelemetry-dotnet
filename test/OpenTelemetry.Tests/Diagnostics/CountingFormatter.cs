// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Diagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// A formatter that counts invocations, for verifying the dispatcher's format-once behavior.
/// </summary>
internal sealed class CountingFormatter : ISelfDiagnosticsFormatter
{
    private int formatCount;

    public int FormatCount => Volatile.Read(ref this.formatCount);

    public string? FileHeader => null;

    public string Format(in SelfDiagnosticsLogEntry entry)
    {
        Interlocked.Increment(ref this.formatCount);
        return entry.Message;
    }
}
