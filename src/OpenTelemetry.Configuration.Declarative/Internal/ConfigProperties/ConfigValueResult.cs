// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// The result of a typed read from <see cref="ConfigProperties"/>, combining a <see cref="ConfigValueOutcome"/>
/// with the typed value when the outcome is <see cref="ConfigValueOutcome.Present"/>.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
internal readonly struct ConfigValueResult<T>
{
    internal ConfigValueResult(ConfigValueOutcome outcome, T? value)
    {
        this.Outcome = outcome;
        this.Value = value;
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
    /// Deconstructs into <paramref name="outcome"/> and <paramref name="value"/>.
    /// </summary>
    /// <param name="outcome">The outcome of the read operation.</param>
    /// <param name="value">The value, or the default for <typeparamref name="T"/> when not <see cref="ConfigValueOutcome.Present"/>.</param>
    public void Deconstruct(out ConfigValueOutcome outcome, out T? value)
    {
        outcome = this.Outcome;
        value = this.Value;
    }
}
