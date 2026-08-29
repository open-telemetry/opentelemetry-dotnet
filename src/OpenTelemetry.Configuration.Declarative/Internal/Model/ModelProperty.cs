// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A three-state wrapper for a declarative-configuration value: <see cref="ModelPropertyState.Absent"/>,
/// <see cref="ModelPropertyState.Null"/>, or <see cref="ModelPropertyState.Present"/>.
/// </summary>
/// <remarks>
/// Nullable value/reference types cannot express the absent-versus-present-null distinction for scalar
/// fields (both collapse to <see langword="null"/>), so this explicit wrapper is used throughout the
/// typed in-memory model. It is a <see langword="readonly"/> <see langword="struct"/> to avoid per-field
/// heap allocation, and its default value is <see cref="ModelPropertyState.Absent"/>.
/// </remarks>
/// <typeparam name="T">The value type.</typeparam>
internal readonly struct ModelProperty<T>
{
    /// <summary>
    /// A <see cref="ModelProperty{T}"/> whose key did not appear in the document.
    /// </summary>
    public static readonly ModelProperty<T> Absent;

    /// <summary>
    /// A <see cref="ModelProperty{T}"/> whose key appeared with a null value.
    /// </summary>
    public static readonly ModelProperty<T> Null = new(ModelPropertyState.Null, default);

    private readonly T? value;

    private ModelProperty(ModelPropertyState state, T? value)
    {
        this.State = state;
        this.value = value;
    }

    /// <summary>
    /// Gets the <see cref="ModelPropertyState"/> of this <see cref="ModelProperty{T}"/>.
    /// </summary>
    public ModelPropertyState State { get; }

    /// <summary>
    /// Gets a value indicating whether the key did not appear in the document.
    /// </summary>
    public bool IsAbsent => this.State == ModelPropertyState.Absent;

    /// <summary>
    /// Gets a value indicating whether the key appeared with a null value.
    /// </summary>
    public bool IsNull => this.State == ModelPropertyState.Null;

    /// <summary>
    /// Gets a value indicating whether the key appeared with a value.
    /// </summary>
    public bool IsPresent => this.State == ModelPropertyState.Present;

    /// <summary>
    /// Gets the present value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the property is not <see cref="ModelPropertyState.Present"/>.
    /// </exception>
    public T Value => this.IsPresent
        ? this.value!
        : throw new InvalidOperationException($"ModelProperty is {this.State} and has no value.");

    /// <summary>
    /// Creates a <see cref="ModelProperty{T}"/> whose key appeared with the supplied <paramref name="value"/>.
    /// </summary>
    /// <param name="value">
    /// The value for the <see cref="ModelProperty{T}"/>.
    /// </param>
    /// <returns>
    /// A <see cref="ModelProperty{T}"/> holding the supplied <paramref name="value"/>.
    /// </returns>
    public static ModelProperty<T> Create(T value) => new(ModelPropertyState.Present, value);

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
