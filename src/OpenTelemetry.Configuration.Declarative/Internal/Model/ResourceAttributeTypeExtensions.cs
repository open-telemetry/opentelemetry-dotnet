// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

internal static class ResourceAttributeTypeExtensions
{
    internal static string GetSchemaName(this ResourceAttributeType type) =>
        type switch
        {
            ResourceAttributeType.String => "string",
            ResourceAttributeType.Boolean => "bool",
            ResourceAttributeType.Integer => "int",
            ResourceAttributeType.Double => "double",
            ResourceAttributeType.StringArray => "string_array",
            ResourceAttributeType.BooleanArray => "bool_array",
            ResourceAttributeType.IntegerArray => "int_array",
            ResourceAttributeType.DoubleArray => "double_array",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
}
