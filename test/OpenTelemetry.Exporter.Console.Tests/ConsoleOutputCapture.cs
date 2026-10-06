// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;

namespace OpenTelemetry.Exporter.Console.Tests;

/// <summary>
/// Redirects stdout and <see cref="System.Diagnostics.Trace"/> output for the lifetime of a
/// test, so tests can observe which <see cref="ConsoleExporterOutputTargets"/> an export
/// actually wrote to.
/// <para/>
/// This mutates process-global static state (<see cref="System.Console.Out"/> and
/// <see cref="System.Diagnostics.Trace.Listeners"/>), so any test class that uses this type
/// must carry <c>[Collection(ConsoleOutputCollectionDefinition.Name)]</c> to guarantee
/// sequential execution, even if repo-wide parallelization settings change.
/// </summary>
internal sealed class ConsoleOutputCapture : IDisposable
{
    private readonly TextWriter originalOut;
    private readonly StringWriter consoleWriter = new();
    private readonly CollectingTraceListener traceListener = new();

    public ConsoleOutputCapture()
    {
        this.originalOut = System.Console.Out;
        System.Console.SetOut(this.consoleWriter);
        System.Diagnostics.Trace.Listeners.Add(this.traceListener);
    }

    public string ConsoleOutput => this.consoleWriter.ToString();

    public string TraceOutput => this.traceListener.Output;

    public void Clear()
    {
        this.consoleWriter.GetStringBuilder().Clear();
        this.traceListener.Clear();
    }

    public void Dispose()
    {
        System.Console.SetOut(this.originalOut);
        System.Diagnostics.Trace.Listeners.Remove(this.traceListener);
        this.traceListener.Dispose();
        this.consoleWriter.Dispose();
    }

    private sealed class CollectingTraceListener : TraceListener
    {
        private readonly StringBuilder builder = new();

        public string Output => this.builder.ToString();

        public void Clear() => this.builder.Clear();

        public override void Write(string? message) => this.builder.Append(message);

        public override void WriteLine(string? message) => this.builder.AppendLine(message);
    }
}
