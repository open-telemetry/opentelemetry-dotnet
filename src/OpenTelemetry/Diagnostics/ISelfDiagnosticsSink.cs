// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Diagnostics;

/// <summary>
/// A destination for self-diagnostics entries. Sinks are simple synchronous writers; all
/// queueing, threading, level filtering, and format-once caching is owned by the
/// <see cref="SelfDiagnosticsSinkDispatcher"/>, which invokes sinks from a single pump thread.
/// </summary>
internal interface ISelfDiagnosticsSink : IDisposable
{
    /// <summary>
    /// Gets the formatter this sink consumes, or <see langword="null"/> when the sink consumes
    /// the raw <see cref="SelfDiagnosticsLogEntry"/> instead of formatted text (e.g. a sink
    /// forwarding to an external <see cref="ILogger"/> pipeline with its own formatting).
    /// </summary>
    ISelfDiagnosticsFormatter? Formatter { get; }

    /// <summary>
    /// Gets a value indicating whether the sink can currently accept entries at the given level.
    /// </summary>
    /// <param name="level">The level being tested.</param>
    /// <returns><see langword="true"/> when the sink would write an entry at this level.</returns>
    bool IsEnabled(LogLevel level);

    /// <summary>
    /// Writes one entry. <paramref name="formatted"/> is the output of this sink's
    /// <see cref="Formatter"/> (shared across sinks using the same formatter instance), or
    /// <see langword="null"/> when <see cref="Formatter"/> is <see langword="null"/>.
    /// </summary>
    /// <param name="entry">The entry being written.</param>
    /// <param name="formatted">The pre-rendered text for formatter-consuming sinks.</param>
    void Write(in SelfDiagnosticsLogEntry entry, string? formatted);

    /// <summary>
    /// Flushes buffered output.
    /// </summary>
    void Flush();
}
