// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The result of a typed read from <see cref="ConfigProperties"/>, combining a
/// <see cref="ConfigValueOutcome"/> with the typed value.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
#pragma warning disable CA1815 // This transient read result is consumed through Outcome and Value, not equality comparisons.
public readonly struct ConfigValueResult<T>
#pragma warning restore CA1815 // Override equals and operator equals on value types
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
}
