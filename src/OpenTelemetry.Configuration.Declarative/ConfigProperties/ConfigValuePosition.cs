// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The line and column at which a configuration value was authored in its source document.
/// </summary>
/// <remarks>
/// Line and column are one-based. The default value means the position is unknown, which is the
/// case for a value that did not come from a text document.
/// </remarks>
#pragma warning disable CA1815 // Override equals and operator equals on value types.Source positions are consumed through their individual members, not equality comparisons.
public readonly struct ConfigValuePosition
#pragma warning restore CA1815 // Override equals and operator equals on value types
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigValuePosition"/> struct.
    /// </summary>
    /// <param name="line">The one-based line number.</param>
    /// <param name="column">The one-based column number.</param>
    internal ConfigValuePosition(long line, long column)
    {
        this.Line = line;
        this.Column = column;
    }

    /// <summary>
    /// Gets a position whose source location is unknown.
    /// </summary>
    public static ConfigValuePosition Unknown => default;

    /// <summary>
    /// Gets the one-based line number, or zero when the position is unknown.
    /// </summary>
    public long Line { get; }

    /// <summary>
    /// Gets the one-based column number, or zero when the position is unknown.
    /// </summary>
    public long Column { get; }

    /// <summary>
    /// Gets a value indicating whether a source position was recorded.
    /// </summary>
    public bool HasPosition => this.Line > 0;
}
