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
/// </remarks>
internal readonly struct ConfigValue
{
    private readonly object? objectPayload;
    private readonly long numericPayload;
    private readonly double doublePayload;

    private ConfigValue(ConfigValueKind kind, object? objectPayload, long numericPayload, double doublePayload, bool isUnrepresentable)
    {
        this.Kind = kind;
        this.objectPayload = objectPayload;
        this.numericPayload = numericPayload;
        this.doublePayload = doublePayload;
        this.IsUnrepresentable = isUnrepresentable;
    }

    /// <summary>
    /// Gets the kind of value stored in this instance.
    /// </summary>
    internal ConfigValueKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether an integer value could not be represented as a <see cref="long"/>.
    /// When <see langword="true"/>, <see cref="AsLong"/> must not be called.
    /// </summary>
    internal bool IsUnrepresentable { get; }

    /// <summary>
    /// Creates a null value.
    /// </summary>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Null"/>.</returns>
    internal static ConfigValue Null() => new(ConfigValueKind.Null, null, 0L, 0.0, false);

    /// <summary>
    /// Creates a string value.
    /// </summary>
    /// <param name="value">The string to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.String"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    internal static ConfigValue String(string value)
    {
        Guard.ThrowIfNull(value);
        return new(ConfigValueKind.String, value, 0L, 0.0, false);
    }

    /// <summary>
    /// Creates a boolean value.
    /// </summary>
    /// <param name="value">The boolean to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Boolean"/>.</returns>
    internal static ConfigValue Boolean(bool value) => new(ConfigValueKind.Boolean, null, value ? 1L : 0L, 0.0, false);

    /// <summary>
    /// Creates an integer value representable as a <see cref="long"/>.
    /// </summary>
    /// <param name="value">The integer to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Integer"/>.</returns>
    internal static ConfigValue Integer(long value) => new(ConfigValueKind.Integer, null, value, 0.0, false);

    /// <summary>
    /// Creates an integer value that cannot be represented as a <see cref="long"/> (e.g. out-of-range).
    /// </summary>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Integer"/> and <see cref="IsUnrepresentable"/> set to <see langword="true"/>.</returns>
    internal static ConfigValue UnrepresentableInteger() => new(ConfigValueKind.Integer, null, 0L, 0.0, true);

    /// <summary>
    /// Creates a floating-point value.
    /// </summary>
    /// <param name="value">The double to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Float"/>.</returns>
    internal static ConfigValue Float(double value) => new(ConfigValueKind.Float, null, 0L, value, false);

    /// <summary>
    /// Creates a mapping value backed by <paramref name="properties"/>.
    /// </summary>
    /// <param name="properties">The <see cref="ConfigProperties"/> to store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Mapping"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="properties"/> is <see langword="null"/>.</exception>
    internal static ConfigValue Mapping(ConfigProperties properties)
    {
        Guard.ThrowIfNull(properties);
        return new(ConfigValueKind.Mapping, properties, 0L, 0.0, false);
    }

    /// <summary>Creates a sequence value from <paramref name="items"/>, snapshotting the list to ensure immutability.</summary>
    /// <param name="items">The items to snapshot and store.</param>
    /// <returns>A <see cref="ConfigValue"/> with <see cref="Kind"/> <see cref="ConfigValueKind.Sequence"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    internal static ConfigValue Sequence(IReadOnlyList<ConfigValue> items)
    {
        Guard.ThrowIfNull(items);

        // Snapshot so a caller retaining a List/array cannot mutate a ConfigProperties that R13 requires
        // to be immutable and shareable across threads.
        var snapshot = new ConfigValue[items.Count];
        for (var i = 0; i < snapshot.Length; i++)
        {
            snapshot[i] = items[i];
        }

        return new(ConfigValueKind.Sequence, Array.AsReadOnly(snapshot), 0L, 0.0, false);
    }

    /// <summary>
    /// Returns the value as a string. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.String"/>.
    /// </summary>
    /// <returns>The stored string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.String"/>.</exception>
    internal string AsString() => this.Kind == ConfigValueKind.String
        ? (string)this.objectPayload!
        : throw new InvalidOperationException($"Cannot read a {this.Kind} value as string.");

    /// <summary>
    /// Returns the value as a boolean. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Boolean"/>.
    /// </summary>
    /// <returns>The stored boolean.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Boolean"/>.</exception>
    internal bool AsBoolean() => this.Kind == ConfigValueKind.Boolean
        ? this.numericPayload != 0
        : throw new InvalidOperationException($"Cannot read a {this.Kind} value as boolean.");

    /// <summary>
    /// Returns the value as a long. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Integer"/> and <see cref="IsUnrepresentable"/> is <see langword="false"/>.
    /// </summary>
    /// <returns>The stored integer.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Integer"/> or <see cref="IsUnrepresentable"/> is <see langword="true"/>.</exception>
    internal long AsLong() => this.Kind == ConfigValueKind.Integer && !this.IsUnrepresentable
        ? this.numericPayload
        : this.Kind != ConfigValueKind.Integer
            ? throw new InvalidOperationException($"Cannot read a {this.Kind} value as long.")
            : throw new InvalidOperationException("Cannot read an out-of-range integer value as long.");

    /// <summary>
    /// Returns the value as a double. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Float"/>.
    /// </summary>
    /// <returns>The stored double.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Float"/>.</exception>
    internal double AsDouble() => this.Kind == ConfigValueKind.Float
        ? this.doublePayload
        : throw new InvalidOperationException($"Cannot read a {this.Kind} value as double.");

    /// <summary>
    /// Returns the value as a <see cref="ConfigProperties"/>. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Mapping"/>.
    /// </summary>
    /// <returns>The stored <see cref="ConfigProperties"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Mapping"/>.</exception>
    internal ConfigProperties AsMapping() => this.Kind == ConfigValueKind.Mapping
        ? (ConfigProperties)this.objectPayload!
        : throw new InvalidOperationException($"Cannot read a {this.Kind} value as mapping.");

    /// <summary>
    /// Returns the value as a read-only list of <see cref="ConfigValue"/>s. Only valid when <see cref="Kind"/> is <see cref="ConfigValueKind.Sequence"/>.
    /// </summary>
    /// <returns>The stored sequence.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Kind"/> is not <see cref="ConfigValueKind.Sequence"/>.</exception>
    internal IReadOnlyList<ConfigValue> AsSequence() => this.Kind == ConfigValueKind.Sequence
        ? (IReadOnlyList<ConfigValue>)this.objectPayload!
        : throw new InvalidOperationException($"Cannot read a {this.Kind} value as sequence.");
}
