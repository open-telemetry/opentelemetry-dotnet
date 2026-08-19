// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.SelfDiagnostics;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Internal;

/// <summary>
/// Writes self-diagnostics entries to standard output and/or standard error.
/// </summary>
/// <remarks>
/// <para>
/// Routing: when only one of <see cref="SelfDiagnosticsOptions.LogToStdout"/> or
/// <see cref="SelfDiagnosticsOptions.LogToStderr"/> is set, all entries go to that stream.
/// When both are set, entries at <see cref="LogLevel.Warning"/>
/// and below go to standard output and entries above
/// <see cref="LogLevel.Warning"/> go to standard error.
/// </para>
/// <para>
/// Writes happen on the <see cref="SelfDiagnosticsSinkDispatcher"/> pump thread, never on the
/// thread that emitted the entry, so a slow or stalled console (e.g. a redirected pipe with a
/// blocked reader) cannot block application threads. The trade-off is that diagnostic lines may
/// interleave out of order with the application's own console writes.
/// </para>
/// </remarks>
internal sealed class SelfDiagnosticsConsoleSink : ISelfDiagnosticsSink
{
    private readonly Func<TextWriter> stdout;
    private readonly Func<TextWriter> stderr;

    // Mutable so hot-reload can retarget the streams without rebuilding the sink.
    private volatile bool logToStdout;
    private volatile bool logToStderr;

    internal SelfDiagnosticsConsoleSink(
        bool logToStdout,
        bool logToStderr,
        Func<TextWriter>? stdout = null,
        Func<TextWriter>? stderr = null)
    {
        this.logToStdout = logToStdout;
        this.logToStderr = logToStderr;
        this.stdout = stdout ?? (static () => Console.Out);
        this.stderr = stderr ?? (static () => Console.Error);
    }

    /// <inheritdoc/>
    public ISelfDiagnosticsFormatter? Formatter => SelfDiagnosticsTextFormatter.Instance;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel level) => this.logToStdout || this.logToStderr;

    /// <inheritdoc/>
    public void Write(in SelfDiagnosticsLogEntry entry, string? formatted)
    {
        try
        {
            var writer = this.SelectWriter(entry.Level);
            writer.WriteLine(formatted);
        }
        catch
        {
            // A console failure (closed stream, broken pipe) must never propagate into the pump.
        }
    }

    /// <inheritdoc/>
    public void Flush()
    {
        // Console writers auto-flush; nothing buffered here.
    }

    /// <inheritdoc/>
    public void OnInstalled()
    {
        // No post-install work needed.
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The sink does not own the console streams.
    }

    /// <summary>
    /// Updates the stdout/stderr routing flags. Called by the hot-reload path when
    /// <see cref="SelfDiagnosticsOptions.LogToStdout"/> or <see cref="SelfDiagnosticsOptions.LogToStderr"/> changes.
    /// </summary>
    /// <param name="logToStdout">New value for <see cref="SelfDiagnosticsOptions.LogToStdout"/>.</param>
    /// <param name="logToStderr">New value for <see cref="SelfDiagnosticsOptions.LogToStderr"/>.</param>
    internal void UpdateConsoleFlags(bool logToStdout, bool logToStderr)
    {
        this.logToStdout = logToStdout;
        this.logToStderr = logToStderr;
    }

    private TextWriter SelectWriter(LogLevel level)
    {
        if (this.logToStderr && (!this.logToStdout || level > LogLevel.Warning))
        {
            return this.stderr();
        }

        return this.stdout();
    }
}
