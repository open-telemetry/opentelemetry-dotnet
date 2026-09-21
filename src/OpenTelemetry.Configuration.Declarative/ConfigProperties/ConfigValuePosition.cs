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
public readonly struct ConfigValuePosition : IEquatable<ConfigValuePosition>
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

    /// <summary>
    /// Returns a value indicating whether two <see cref="ConfigValuePosition"/> instances are equal.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns><see langword="true"/> if the instances are equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(ConfigValuePosition left, ConfigValuePosition right) => left.Equals(right);

    /// <summary>
    /// Returns a value indicating whether two <see cref="ConfigValuePosition"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns><see langword="true"/> if the instances are not equal; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(ConfigValuePosition left, ConfigValuePosition right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ConfigValuePosition other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(this.Line, this.Column);
#else
        var hash = 17;
        unchecked
        {
            hash = (31 * hash) + this.Line.GetHashCode();
            hash = (31 * hash) + this.Column.GetHashCode();
        }

        return hash;
#endif
    }

    /// <inheritdoc/>
    public bool Equals(ConfigValuePosition other) => this.Line == other.Line && this.Column == other.Column;
}
