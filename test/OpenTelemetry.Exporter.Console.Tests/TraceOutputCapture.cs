// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Console.Tests;

/// <summary>
/// Captures what the exporters write to <see cref="ConsoleExporterOutputTargets.Debug"/>
/// so that tests can assert on the output.
/// </summary>
/// <remarks>
/// Only lines written by the thread that created the instance are captured, so
/// tests which run in parallel do not see each other's output. Unlike redirecting
/// <see cref="Console.Out"/>, no process-wide state needs to be restored.
/// </remarks>
internal sealed class TraceOutputCapture : TraceListener
{
    private readonly int threadId = Environment.CurrentManagedThreadId;
    private readonly List<string> lines = [];

    public TraceOutputCapture()
    {
        System.Diagnostics.Trace.Listeners.Add(this);
    }

    public IReadOnlyList<string> Lines
    {
        get
        {
            lock (this.lines)
            {
                return [.. this.lines];
            }
        }
    }

    public override void Write(string? message)
        => this.Capture(message);

    public override void WriteLine(string? message)
        => this.Capture(message);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            System.Diagnostics.Trace.Listeners.Remove(this);
        }

        base.Dispose(disposing);
    }

    private void Capture(string? message)
    {
        if (Environment.CurrentManagedThreadId != this.threadId)
        {
            return;
        }

        lock (this.lines)
        {
            this.lines.Add(message ?? string.Empty);
        }
    }
}
