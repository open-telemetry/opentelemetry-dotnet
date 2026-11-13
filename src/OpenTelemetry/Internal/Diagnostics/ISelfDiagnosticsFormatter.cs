// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// Renders a <see cref="SelfDiagnosticsLogEntry"/> to text for a sink.
/// </summary>
internal interface ISelfDiagnosticsFormatter
{
    /// <summary>
    /// Gets an optional header line written at the top of each new log file (after the
    /// preamble), or <see langword="null"/> when the format has no header concept.
    /// </summary>
    string? FileHeader { get; }

    /// <summary>
    /// Renders the entry, including its exception if present, as a single output string.
    /// </summary>
    /// <param name="entry">The entry to render.</param>
    /// <returns>The rendered text, without a trailing newline.</returns>
    string Format(in SelfDiagnosticsLogEntry entry);
}
