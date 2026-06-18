// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A three-state wrapper for a declarative-configuration value: <see cref="ConfigPropertyState.Absent"/>,
/// <see cref="ConfigPropertyState.Null"/>, or <see cref="ConfigPropertyState.Present"/>.
/// </summary>
/// <remarks>
/// Nullable value/reference types cannot express the absent-versus-present-null distinction for scalar
/// fields (both collapse to <see langword="null"/>), so this explicit wrapper is used throughout the
/// typed in-memory model. It is a <see langword="readonly"/> <see langword="struct"/> to avoid per-field
/// heap allocation, and its default value is <see cref="ConfigPropertyState.Absent"/>.
/// </remarks>
/// <typeparam name="T">The value type.</typeparam>
internal readonly struct ConfigProperty<T>
{
    /// <summary>
    /// A <see cref="ConfigProperty{T}"/> whose key did not appear in the document.
    /// </summary>
    public static readonly ConfigProperty<T> Absent;

    /// <summary>
    /// A <see cref="ConfigProperty{T}"/> whose key appeared with a null value.
    /// </summary>
    public static readonly ConfigProperty<T> Null = new(ConfigPropertyState.Null, default);

    private readonly T? value;

    private ConfigProperty(ConfigPropertyState state, T? value)
    {
        this.State = state;
        this.value = value;
    }

    /// <summary>
    /// Gets the <see cref="ConfigPropertyState"/> of this <see cref="ConfigProperty{T}"/>.
    /// </summary>
    public ConfigPropertyState State { get; }

    /// <summary>
    /// Gets a value indicating whether the key did not appear in the document.
    /// </summary>
    public bool IsAbsent => this.State == ConfigPropertyState.Absent;

    /// <summary>
    /// Gets a value indicating whether the key appeared with a null value.
    /// </summary>
    public bool IsNull => this.State == ConfigPropertyState.Null;

    /// <summary>
    /// Gets a value indicating whether the key appeared with a value.
    /// </summary>
    public bool IsPresent => this.State == ConfigPropertyState.Present;

    /// <summary>
    /// Gets the present value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the property is not <see cref="ConfigPropertyState.Present"/>.
    /// </exception>
    public T Value => this.IsPresent
        ? this.value!
        : throw new InvalidOperationException($"ConfigProperty is {this.State} and has no value.");

    /// <summary>
    /// Creates a <see cref="ConfigProperty{T}"/> whose key appeared with the supplied <paramref name="value"/>.
    /// </summary>
    /// <param name="value">
    /// The value for the <see cref="ConfigProperty{T}"/>.
    /// </param>
    /// <returns>
    /// A <see cref="ConfigProperty{T}"/> holding the supplied <paramref name="value"/>.
    /// </returns>
    public static ConfigProperty<T> Create(T value) => new(ConfigPropertyState.Present, value);

    /// <summary>
    /// Gets the present value if there is one.
    /// </summary>
    /// <param name="value">
    /// When this method returns <see langword="true"/>, the present value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the property is present; otherwise <see langword="false"/>.
    /// </returns>
    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        if (this.IsPresent)
        {
            value = this.value!;
            return true;
        }

        value = default;
        return false;
    }
}
