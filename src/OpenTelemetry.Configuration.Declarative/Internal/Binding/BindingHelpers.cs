// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Shared helpers for the binders that read configuration sections from a document's properties.
/// </summary>
internal static class BindingHelpers
{
    /// <summary>
    /// Creates an exception whose message starts with the quoted path and, when known, the line and column.
    /// </summary>
    /// <param name="path">The document path of the offending value.</param>
    /// <param name="position">The position of the offending value, which may be unknown.</param>
    /// <param name="message">The description of the problem.</param>
    /// <returns>The exception to throw.</returns>
    internal static DeclarativeConfigurationException CreateException(string path, ConfigValuePosition position, string message) =>
        new($"{YamlPath.Describe(path, position)} {message}");

    /// <summary>
    /// Returns the value's own position, falling back to the key's position when the value has none (for example, an empty value).
    /// </summary>
    /// <param name="parent">The properties that hold the key.</param>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns>The best available position.</returns>
    internal static ConfigValuePosition PositionOfValueOrKey(ConfigProperties parent, string key, ConfigValue value) =>
        value.Position.HasPosition ? value.Position : parent.GetKeyPosition(key);

    /// <summary>
    /// Returns the value as a mapping, or throws when it is not one.
    /// </summary>
    /// <param name="parent">The properties that hold the key.</param>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <param name="path">The document path of the value.</param>
    /// <returns>The mapping.</returns>
    /// <exception cref="DeclarativeConfigurationException">The value is not a mapping.</exception>
    internal static ConfigProperties AsRequiredMapping(ConfigProperties parent, string key, ConfigValue value, string path)
    {
        if (value.Kind != ConfigValueKind.Mapping)
        {
            throw CreateException(path, PositionOfValueOrKey(parent, key, value), "must be a mapping.");
        }

        return value.AsMapping();
    }

    /// <summary>
    /// Returns the value as a sequence with at least one item, or throws when it is not one.
    /// </summary>
    /// <param name="parent">The properties that hold the key.</param>
    /// <param name="key">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <param name="path">The document path of the value.</param>
    /// <param name="itemDescription">Describes one item, for use in the error message.</param>
    /// <returns>The sequence items.</returns>
    /// <exception cref="DeclarativeConfigurationException">The value is not a non-empty sequence.</exception>
    internal static IReadOnlyList<ConfigValue> AsRequiredNonEmptySequence(ConfigProperties parent, string key, ConfigValue value, string path, string itemDescription)
    {
        if (value.Kind == ConfigValueKind.Sequence && value.AsSequence() is { Count: > 0 } items)
        {
            return items;
        }

        throw CreateException(path, PositionOfValueOrKey(parent, key, value), $"must be a list of at least one {itemDescription}.");
    }

    /// <summary>
    /// Reads the <c>enabled</c> property of a mapping, which defaults to <see langword="true"/> when absent.
    /// </summary>
    /// <param name="config">The mapping that may hold <c>enabled</c>.</param>
    /// <param name="configPath">The document path of the mapping.</param>
    /// <returns>The <c>enabled</c> value.</returns>
    /// <exception cref="DeclarativeConfigurationException">The value is not a boolean.</exception>
    internal static bool ReadEnabled(ConfigProperties config, string configPath)
    {
        var result = config.GetBoolean(YamlKeys.Enabled);
        return result.Outcome switch
        {
            ConfigValueOutcome.Absent => true,
            ConfigValueOutcome.Present => result.Value,
            _ => throw CreateException(YamlPath.Child(configPath, YamlKeys.Enabled), result.Position, "must be true or false."),
        };
    }

    /// <summary>
    /// Binds a component node: a mapping with exactly one key, the component name, whose value is a
    /// mapping or null. A null value means the component has no properties.
    /// </summary>
    /// <param name="value">The node to bind.</param>
    /// <param name="path">The document path of the node.</param>
    /// <param name="description">Describes the kind of component, for use in the error message.</param>
    /// <param name="keyPosition">The position of the key that holds the node, used when the node has no position.</param>
    /// <returns>The bound component.</returns>
    /// <exception cref="DeclarativeConfigurationException">The node is not a valid component.</exception>
    internal static ComponentConfiguration BindComponent(
        ConfigValue value,
        string path,
        string description,
        ConfigValuePosition keyPosition = default)
    {
        var position = value.Position.HasPosition ? value.Position : keyPosition;

        if (value.Kind != ConfigValueKind.Mapping || value.AsMapping().Keys.Count != 1)
        {
            throw CreateException(path, position, $"must name exactly one {description}.");
        }

        var mapping = value.AsMapping();
        var name = mapping.Keys.First();
        var componentPath = YamlPath.Child(path, name);
        var componentPosition = mapping.GetKeyPosition(name);

        _ = mapping.TryGetValue(name, out var componentValue);

        var properties = componentValue.Kind switch
        {
            ConfigValueKind.Null => ConfigProperties.Empty,
            ConfigValueKind.Mapping => componentValue.AsMapping(),
            _ => throw CreateException(componentPath, PositionOfValueOrKey(mapping, name, componentValue), "must be a mapping or null."),
        };

        return new(name, properties, componentPath, componentPosition);
    }

    /// <summary>
    /// Reads an integer property. An absent or null value takes the default; <see cref="IntegerSetting.IsSpecified"/> records whether the user supplied one.
    /// </summary>
    /// <param name="properties">The mapping that may hold the property.</param>
    /// <param name="parentPath">The document path of the mapping.</param>
    /// <param name="key">The property name.</param>
    /// <param name="defaultValue">The value to use when the property is absent or null.</param>
    /// <param name="minimum">The smallest accepted value.</param>
    /// <returns>The value and where it came from.</returns>
    /// <exception cref="DeclarativeConfigurationException">The value is not an integer in the accepted range.</exception>
    internal static IntegerSetting ReadInteger(ConfigProperties properties, string parentPath, string key, int defaultValue, IntegerMinimum minimum)
    {
        var result = properties.GetInt(key);
        var path = YamlPath.Child(parentPath, key);
        var minimumValue = (int)minimum;
        var range = minimum == IntegerMinimum.Zero ? "a non-negative integer" : "a positive integer";

        return result.Outcome switch
        {
            ConfigValueOutcome.Absent or ConfigValueOutcome.PresentNull => new(defaultValue, result.Position, IsSpecified: false),
            ConfigValueOutcome.Present when result.Value >= minimumValue => new(result.Value, result.Position, IsSpecified: true),
            ConfigValueOutcome.Present => throw CreateException(path, result.Position, $"must be {range}."),
            _ => throw CreateException(path, result.Position, FormattableString.Invariant($"must be {range} no larger than {int.MaxValue}.")),
        };
    }
}
