// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration;

/// <summary>
/// A mutable builder that accumulates key/value pairs and produces an immutable <see cref="ConfigProperties"/>.
/// </summary>
internal sealed class ConfigPropertiesBuilder
{
    private readonly Dictionary<string, ConfigValue> values = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a null value.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <returns>This builder, for chaining.</returns>
    public ConfigPropertiesBuilder AddNull(string key)
        => this.AddValue(key, ConfigValue.Null);

    /// <summary>
    /// Adds a string value.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>This builder, for chaining.</returns>
    public ConfigPropertiesBuilder Add(string key, string value)
        => this.AddValue(key, ConfigValue.String(value));

    /// <summary>
    /// Adds a boolean value.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>This builder, for chaining.</returns>
    public ConfigPropertiesBuilder Add(string key, bool value)
        => this.AddValue(key, ConfigValue.Boolean(value));

    /// <summary>
    /// Adds a 32-bit integer value.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>This builder, for chaining.</returns>
    public ConfigPropertiesBuilder Add(string key, int value)
        => this.AddValue(key, ConfigValue.Integer(value));

    /// <summary>
    /// Adds a 64-bit integer value.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>This builder, for chaining.</returns>
    public ConfigPropertiesBuilder Add(string key, long value)
        => this.AddValue(key, ConfigValue.Integer(value));

    /// <summary>
    /// Adds a double-precision floating-point value.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>This builder, for chaining.</returns>
    public ConfigPropertiesBuilder Add(string key, double value)
        => this.AddValue(key, ConfigValue.Double(value));

    /// <summary>
    /// Adds a nested mapping.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>This builder, for chaining.</returns>
    public ConfigPropertiesBuilder Add(string key, ConfigProperties value)
        => this.AddValue(key, ConfigValue.Mapping(value));

    /// <summary>
    /// Adds a sequence of scalar values.
    /// </summary>
    /// <typeparam name="T">
    /// The scalar element type. Supported types are <see cref="string"/>, <see cref="bool"/>,
    /// <see cref="long"/>, <see cref="double"/>, and <see cref="int"/>.
    /// </typeparam>
    /// <param name="key">The key to add.</param>
    /// <param name="items">The sequence items.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when <typeparamref name="T"/> is not a supported scalar type.
    /// </exception>
    public ConfigPropertiesBuilder AddScalarList<T>(string key, IReadOnlyList<T> items)
    {
        Guard.ThrowIfNull(items);

        var values = new ConfigValue[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = ToScalarValue(items[i]);
        }

        return this.AddValue(key, ConfigValue.Sequence(values));
    }

    /// <summary>
    /// Adds a sequence of nested mappings.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="items">The nested mappings.</param>
    /// <returns>This builder, for chaining.</returns>
    public ConfigPropertiesBuilder AddPropertiesList(
        string key,
        IReadOnlyList<ConfigProperties> items)
    {
        Guard.ThrowIfNull(items);

        var values = new ConfigValue[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            values[i] = ConfigValue.Mapping(items[i]);
        }

        return this.AddValue(key, ConfigValue.Sequence(values));
    }

    /// <summary>
    /// Creates an immutable <see cref="ConfigProperties"/> from the accumulated entries.
    /// </summary>
    /// <returns>A new <see cref="ConfigProperties"/> containing the entries added so far.</returns>
    public ConfigProperties Build()
        => ConfigProperties.Create(this.values);

    /// <summary>
    /// Adds <paramref name="key"/> with an internal configuration value.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> already exists in this builder.</exception>
    internal ConfigPropertiesBuilder Add(string key, ConfigValue value)
        => this.AddValue(key, value);

    private static ConfigValue ToScalarValue<T>(T value)
        => value switch
        {
            string stringValue => ConfigValue.String(stringValue),
            bool booleanValue => ConfigValue.Boolean(booleanValue),
            int integerValue => ConfigValue.Integer(integerValue),
            long integerValue => ConfigValue.Integer(integerValue),
            double doubleValue => ConfigValue.Double(doubleValue),
            _ => throw new NotSupportedException(
                $"'{typeof(T).Name}' is not a supported scalar element type. Supported types are string, bool, long, double, and int."),
        };

    private ConfigPropertiesBuilder AddValue(string key, ConfigValue value)
    {
        this.values.Add(key, value);
        return this;
    }
}
