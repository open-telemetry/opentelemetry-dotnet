// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Maps a <see cref="ResourceAttributeEntry"/> to a key/value pair suitable for constructing a
/// <see cref="Resources.Resource"/>.
/// </summary>
internal static class ResourceAttributeMapper
{
    /// <summary>
    /// Maps a declarative resource attribute to a key/value pair.
    /// </summary>
    /// <param name="entry">The attribute to map.</param>
    /// <returns>
    /// The mapped attribute, or <see langword="null"/> when the attribute must be skipped because
    /// its value is null or contains an integer outside the <see cref="long"/> range.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="entry"/> has a value that is inconsistent with its declared type.
    /// </exception>
    internal static KeyValuePair<string, object>? TryMap(ResourceAttributeEntry entry)
    {
        if (entry.ValueNodeKind == AttributeValueNodeKind.NullScalar)
        {
            // Present-null: the schema's nullBehavior for an attribute value is to ignore the entry.
            OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(
                $"A resource.attributes entry for '{entry.Name}' has a null 'value' field and will be skipped.");
            return null;
        }

        return entry.Type switch
        {
            ResourceAttributeType.Boolean => MapBoolean(entry),
            ResourceAttributeType.BooleanArray => MapBooleanArray(entry),
            ResourceAttributeType.Double => MapDouble(entry),
            ResourceAttributeType.DoubleArray => MapDoubleArray(entry),
            ResourceAttributeType.Integer => MapInteger(entry),
            ResourceAttributeType.IntegerArray => MapIntegerArray(entry),
            ResourceAttributeType.String => new KeyValuePair<string, object>(entry.Name, GetScalar(entry).Value),
            ResourceAttributeType.StringArray => MapStringArray(entry),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ResourceAttributeType)}: {entry.Type}."),
        };
    }

    private static ResolvedYamlScalar GetScalar(ResourceAttributeEntry entry) =>
        entry.TryGetScalarValue(out var value) && entry.ScalarKind.HasValue
            ? new ResolvedYamlScalar(value, entry.ScalarKind.Value)
            : throw new InvalidOperationException(
                $"Attribute '{entry.Name}' declares type '{entry.Type.GetSchemaName()}' but carries no scalar value.");

    private static IReadOnlyList<ResolvedYamlScalar> GetSequence(ResourceAttributeEntry entry) =>
        entry.TryGetSequenceValues(out var values)
            ? values
            : throw new InvalidOperationException(
                $"Attribute '{entry.Name}' declares type '{entry.Type.GetSchemaName()}' but carries no sequence value.");

    // Integer-kind YAML scalars may arrive where the declared type is double. Converting them as
    // Float saturates values beyond the long range to infinity, whereas Integer kind would yield
    // UnrepresentableInteger and make AsDouble throw.
    private static ResolvedYamlScalar AsFloatKind(ResolvedYamlScalar scalar) =>
        scalar.Kind == YamlScalarKind.Integer
            ? scalar with { Kind = YamlScalarKind.Float }
            : scalar;

    private static KeyValuePair<string, object> MapBoolean(ResourceAttributeEntry entry) =>
        new(entry.Name, YamlScalarConverter.Convert(GetScalar(entry)).AsBoolean());

    private static KeyValuePair<string, object> MapBooleanArray(ResourceAttributeEntry entry)
    {
        var items = GetSequence(entry);
        var result = new bool[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            result[i] = YamlScalarConverter.Convert(items[i]).AsBoolean();
        }

        return new(entry.Name, result);
    }

    private static KeyValuePair<string, object> MapDouble(ResourceAttributeEntry entry) =>
        new(entry.Name, YamlScalarConverter.Convert(AsFloatKind(GetScalar(entry))).AsDouble());

    private static KeyValuePair<string, object> MapDoubleArray(ResourceAttributeEntry entry)
    {
        var items = GetSequence(entry);
        var result = new double[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            result[i] = YamlScalarConverter.Convert(AsFloatKind(items[i])).AsDouble();
        }

        return new(entry.Name, result);
    }

    private static KeyValuePair<string, object>? MapInteger(ResourceAttributeEntry entry)
    {
        var scalar = GetScalar(entry);
        var converted = YamlScalarConverter.Convert(scalar);
        if (converted.IsUnrepresentable)
        {
            OpenTelemetryDeclarativeConfigurationEventSource.Log.UnrepresentableResourceAttributeInteger(
                entry.Name, scalar.Value);
            return null;
        }

        return new KeyValuePair<string, object>(entry.Name, converted.AsLong());
    }

    private static KeyValuePair<string, object>? MapIntegerArray(ResourceAttributeEntry entry)
    {
        var items = GetSequence(entry);
        var result = new long[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var converted = YamlScalarConverter.Convert(items[i]);
            if (converted.IsUnrepresentable)
            {
                OpenTelemetryDeclarativeConfigurationEventSource.Log.UnrepresentableResourceAttributeInteger(
                    entry.Name, items[i].Value);
                return null;
            }

            result[i] = converted.AsLong();
        }

        return new KeyValuePair<string, object>(entry.Name, result);
    }

    private static KeyValuePair<string, object> MapStringArray(ResourceAttributeEntry entry)
    {
        var items = GetSequence(entry);
        var result = new string[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            result[i] = items[i].Value;
        }

        return new(entry.Name, result);
    }
}
