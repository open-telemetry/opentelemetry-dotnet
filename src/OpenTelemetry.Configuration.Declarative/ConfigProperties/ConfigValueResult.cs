// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The result of a typed read from <see cref="ConfigProperties"/>, combining a
/// <see cref="ConfigValueOutcome"/> with the typed value.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public readonly struct ConfigValueResult<T> : IEquatable<ConfigValueResult<T>>
{
    internal ConfigValueResult(ConfigValueOutcome outcome, T? value, ConfigValuePosition position)
    {
        this.Outcome = outcome;
        this.Value = value;
        this.Position = position;
    }

    /// <summary>
    /// Gets the outcome of the read operation.
    /// </summary>
    public ConfigValueOutcome Outcome { get; }

    /// <summary>
    /// Gets the value when <see cref="Outcome"/> is <see cref="ConfigValueOutcome.Present"/>; otherwise the default for <typeparamref name="T"/>.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets the position at which the property was authored, or the default when the property is
    /// absent or its position is unknown.
    /// </summary>
    public ConfigValuePosition Position { get; }

    /// <summary>
    /// Returns a value indicating whether two <see cref="ConfigValueResult{T}"/> instances are equal.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns><see langword="true"/> if the instances are equal; otherwise <see langword="false"/>.</returns>
    public static bool operator ==(ConfigValueResult<T> left, ConfigValueResult<T> right) => left.Equals(right);

    /// <summary>
    /// Returns a value indicating whether two <see cref="ConfigValueResult{T}"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <returns><see langword="true"/> if the instances are not equal; otherwise <see langword="false"/>.</returns>
    public static bool operator !=(ConfigValueResult<T> left, ConfigValueResult<T> right) => !left.Equals(right);

    /// <summary>
    /// Deconstructs into <paramref name="outcome"/> and <paramref name="value"/>.
    /// </summary>
    /// <param name="outcome">The outcome of the read operation.</param>
    /// <param name="value">The value, or the default for <typeparamref name="T"/> when not <see cref="ConfigValueOutcome.Present"/>.</param>
    public void Deconstruct(out ConfigValueOutcome outcome, out T? value)
    {
        outcome = this.Outcome;
        value = this.Value;
    }

    /// <summary>
    /// Attempts to get the value when <see cref="Outcome"/> is
    /// <see cref="ConfigValueOutcome.Present"/>.
    /// </summary>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, the value; otherwise, the default for
    /// <typeparamref name="T"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <see cref="Outcome"/> is <see cref="ConfigValueOutcome.Present"/>;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = this.Value;
        return this.Outcome == ConfigValueOutcome.Present;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ConfigValueResult<T> other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(this.Outcome, this.Value, this.Position);
#else
        var hash = 17;
        unchecked
        {
            hash = (31 * hash) + this.Outcome.GetHashCode();
            hash = (31 * hash) + (this.Value?.GetHashCode() ?? 0);
            hash = (31 * hash) + this.Position.GetHashCode();
        }

        return hash;
#endif
    }

    /// <inheritdoc/>
    public bool Equals(ConfigValueResult<T> other)
        => this.Outcome == other.Outcome
        && EqualityComparer<T>.Default.Equals(this.Value!, other.Value!)
        && this.Position == other.Position;
}
