// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration;

/// <summary>
/// A discriminated-union value that can hold any type representable in the OTel configuration schema.
/// </summary>
/// <remarks>
/// Use the <c>As*</c> accessors only after confirming <see cref="Kind"/>. Calling the wrong accessor
/// throws <see cref="InvalidOperationException"/>. Factory methods are the only way to construct an instance.
/// This is a hand-rolled discriminated union; it is intended to migrate to a C# union type once that
/// language feature lands.
/// </remarks>
internal readonly struct ConfigValue
{
    /// <summary>
    /// The default instance. <see cref="ConfigValueKind.Null"/> has value 0, so zero-initializing the struct produces a null value.
    /// </summary>
    internal static readonly ConfigValue Null;

    private static readonly object UnrepresentableIntegerPayload = new();

    private readonly object? payload;

    private ConfigValue(ConfigValueKind kind, object? payload)
    {
        this.Kind = kind;
        this.payload = payload;
    }

    /// <summary>
    /// Gets the kind of value stored in this instance.
    /// </summary>
    internal ConfigValueKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether an integer value could not be represented as a <see cref="long"/>.
    /// When <see langword="true"/>, <see cref="AsLong"/> must not be called.
    /// </summary>
    internal bool IsUnrepresentable =>
        this.Kind == ConfigValueKind.Integer
        && ReferenceEquals(this.payload, UnrepresentableIntegerPayload);

    /// <summary>
    /// Creates a string value.
    /// </summary>
    /// <param name="value">The string to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.String"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    internal static ConfigValue String(string value)
    {
        Guard.ThrowIfNull(value);
        return new(ConfigValueKind.String, value);
    }

    /// <summary>
    /// Creates a boolean value.
    /// </summary>
    /// <param name="value">The boolean to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Boolean"/>.</returns>
    internal static ConfigValue Boolean(bool value) => new(ConfigValueKind.Boolean, value);

    /// <summary>
    /// Creates an integer value representable as a <see cref="long"/>.
    /// </summary>
    /// <param name="value">The integer to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Integer"/>.</returns>
    internal static ConfigValue Integer(long value) => new(ConfigValueKind.Integer, value);

    /// <summary>
    /// Creates an integer value that cannot be represented as a <see cref="long"/> (e.g. out-of-range).
    /// </summary>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Integer"/> and <see cref="IsUnrepresentable"/> set to <see langword="true"/>.</returns>
    internal static ConfigValue UnrepresentableInteger() => new(ConfigValueKind.Integer, UnrepresentableIntegerPayload);

    /// <summary>
    /// Creates a double-precision floating-point value.
    /// </summary>
    /// <param name="value">The double to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Double"/>.</returns>
    internal static ConfigValue Double(double value) => new(ConfigValueKind.Double, value);

    /// <summary>
    /// Creates a mapping value backed by <paramref name="properties"/>.
    /// </summary>
    /// <param name="properties">The <see cref="ConfigProperties"/> to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Mapping"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="properties"/> is <see langword="null"/>.</exception>
    internal static ConfigValue Mapping(ConfigProperties properties)
    {
        Guard.ThrowIfNull(properties);
        return new(ConfigValueKind.Mapping, properties);
    }

    /// <summary>
    /// Creates a sequence value from <paramref name="items"/>.
    /// </summary>
    /// <remarks>
    /// The list is snapshotted; callers may safely mutate <paramref name="items"/> after this call.
    /// </remarks>
    /// <param name="items">The items to snapshot and store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Sequence"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    internal static ConfigValue Sequence(IReadOnlyList<ConfigValue> items)
    {
        Guard.ThrowIfNull(items);

        // Snapshot so a caller retaining the original list or array cannot mutate this value,
        // which must be immutable and thread-safe once constructed.
        var snapshot = new ConfigValue[items.Count];
        for (var i = 0; i < snapshot.Length; i++)
        {
            snapshot[i] = items[i];
        }

        return new(ConfigValueKind.Sequence, Array.AsReadOnly(snapshot));
    }

    /// <summary>
    /// Returns the value as a string. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.String"/>.
    /// </summary>
    /// <returns>The stored string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.String"/>.</exception>
    internal string AsString() => this.GetValue<string>(ConfigValueKind.String);

    /// <summary>
    /// Returns the value as a boolean. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Boolean"/>.
    /// </summary>
    /// <returns>The stored boolean.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Boolean"/>.</exception>
    internal bool AsBoolean() => this.GetValue<bool>(ConfigValueKind.Boolean);

    /// <summary>
    /// Returns the value as a long. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Integer"/> and <see cref="IsUnrepresentable"/> is <see langword="false"/>.
    /// </summary>
    /// <returns>The stored integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Integer"/> or <see cref="IsUnrepresentable"/> is <see langword="true"/>.</exception>
    internal long AsLong()
    {
        if (this.IsUnrepresentable)
        {
            throw new InvalidOperationException("Cannot read an out-of-range integer value as long.");
        }

        return this.GetValue<long>(ConfigValueKind.Integer);
    }

    /// <summary>
    /// Returns the value as a double. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Double"/>.
    /// </summary>
    /// <returns>The stored double.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Double"/>.</exception>
    internal double AsDouble() => this.GetValue<double>(ConfigValueKind.Double);

    /// <summary>
    /// Returns the value as a <see cref="ConfigProperties"/>. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Mapping"/>.
    /// </summary>
    /// <returns>The stored <see cref="ConfigProperties"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Mapping"/>.</exception>
    internal ConfigProperties AsMapping() => this.GetValue<ConfigProperties>(ConfigValueKind.Mapping);

    /// <summary>
    /// Returns the value as a read-only list of <see cref="ConfigValue"/>s. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Sequence"/>.
    /// </summary>
    /// <returns>The stored sequence.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Sequence"/>.</exception>
    internal IReadOnlyList<ConfigValue> AsSequence() => this.GetValue<IReadOnlyList<ConfigValue>>(ConfigValueKind.Sequence);

    private T GetValue<T>(ConfigValueKind expectedKind) => this.Kind == expectedKind && this.payload is T value
        ? value
        : throw new InvalidOperationException($"Cannot read a {this.Kind} value as {expectedKind}.");
}
