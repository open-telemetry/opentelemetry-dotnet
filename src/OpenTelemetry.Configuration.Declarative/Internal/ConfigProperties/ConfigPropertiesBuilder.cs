// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration;

/// <summary>
/// A mutable builder that accumulates key/value pairs and produces an immutable <see cref="ConfigProperties"/>.
/// </summary>
internal sealed class ConfigPropertiesBuilder
{
    private readonly Dictionary<string, ConfigValue> values = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds <paramref name="key"/> with <paramref name="value"/>.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to associate with the key.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> already exists in this builder.</exception>
    public ConfigPropertiesBuilder Add(string key, ConfigValue value)
    {
        this.values.Add(key, value);
        return this;
    }

    /// <summary>
    /// Creates an immutable <see cref="ConfigProperties"/> from the accumulated entries.
    /// </summary>
    /// <returns>A new <see cref="ConfigProperties"/> containing the entries added so far.</returns>
    public ConfigProperties Build()
        => ConfigProperties.Create(this.values);
}
