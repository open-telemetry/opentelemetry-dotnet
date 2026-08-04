// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// Records every entry written to it, along with the formatted text the dispatcher supplied.
/// </summary>
internal sealed class TestSink : ISelfDiagnosticsSink
{
    private readonly ConcurrentQueue<(SelfDiagnosticsLogEntry Entry, string? Formatted)> written = new();
    private int flushCount;
    private int writeThreadId;
    private int disposeThreadId;

    public TestSink(ISelfDiagnosticsFormatter? formatter = null)
    {
        this.Formatter = formatter;
    }

    public ISelfDiagnosticsFormatter? Formatter { get; }

    public bool Enabled { get; set; } = true;

    public int FlushCount => Volatile.Read(ref this.flushCount);

    public int WriteThreadId => Volatile.Read(ref this.writeThreadId);

    public int DisposeThreadId => Volatile.Read(ref this.disposeThreadId);

    public bool Disposed { get; private set; }

    public IReadOnlyList<(SelfDiagnosticsLogEntry Entry, string? Formatted)> Written => [.. this.written];

    public bool IsEnabled(LogLevel level) => this.Enabled;

    public void Write(in SelfDiagnosticsLogEntry entry, string? formatted)
    {
        Volatile.Write(ref this.writeThreadId, Environment.CurrentManagedThreadId);
        this.written.Enqueue((entry, formatted));
    }

    public void OnInstalled()
    {
    }

    public void Flush() => Interlocked.Increment(ref this.flushCount);

    public void Dispose()
    {
        Volatile.Write(ref this.disposeThreadId, Environment.CurrentManagedThreadId);
        this.Disposed = true;
    }
}
