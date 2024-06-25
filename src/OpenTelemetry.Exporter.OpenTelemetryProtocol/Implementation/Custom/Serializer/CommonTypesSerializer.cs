// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Globalization;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Custom.Serializer;

internal static class CommonTypesSerializer
{
    internal static int SerializeResource(ref byte[] buffer, int cursor, Resource resource, int maxAttributeValueLength)
    {
        if (resource != null && resource != Resource.Empty)
        {
            var resourceSize = CommonTypesSizeCalculator.ComputeResourceSize(resource, maxAttributeValueLength);
            if (resourceSize > 0)
            {
                cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, resourceSize, FieldNumberConstants.ResourceSpans_resource, WireType.LEN);
                foreach (var attribute in resource.Attributes)
                {
                    var tagSize = CommonTypesSizeCalculator.ComputeKeyValuePairSize(attribute!, maxAttributeValueLength);
                    cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, tagSize, FieldNumberConstants.Resource_attributes, WireType.LEN);
                    cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.KeyValue_key, attribute.Key);
                    cursor = SerializeAnyValue(ref buffer, cursor, attribute.Value, FieldNumberConstants.KeyValue_value, maxAttributeValueLength);
                }
            }
        }

        return cursor;
    }

    internal static int SerializeKeyValuePair(ref byte[] buffer, int cursor, int fieldNumber, KeyValuePair<string, object?> tag, int maxAttributeValueLength)
    {
        var tagSize = CommonTypesSizeCalculator.ComputeKeyValuePairSize(tag, maxAttributeValueLength);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, tagSize, fieldNumber, WireType.LEN);
        cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.KeyValue_key, tag.Key);
        cursor = SerializeAnyValue(ref buffer, cursor, tag.Value, FieldNumberConstants.KeyValue_value, maxAttributeValueLength);

        return cursor;
    }

    internal static int SerializeArray(ref byte[] buffer, int cursor, Array array, int maxAttributeValueLength)
    {
        var arraySize = CommonTypesSizeCalculator.ComputeArrayValueSize(array, maxAttributeValueLength);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, arraySize, FieldNumberConstants.AnyValue_array_value, WireType.LEN);
        foreach (var ar in array)
        {
            cursor = SerializeAnyValue(ref buffer, cursor, ar, FieldNumberConstants.ArrayValue_Value, maxAttributeValueLength);
        }

        return cursor;
    }

    internal static int SerializeAnyValue(ref byte[] buffer, int cursor, object? value, int fieldNumber, int maxAttributeValueLength)
    {
        var anyValueSize = CommonTypesSizeCalculator.ComputeAnyValueSize(value, maxAttributeValueLength);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, anyValueSize, fieldNumber, WireType.LEN);
        if (value == null)
        {
            return cursor;
        }

        switch (value)
        {
            case char:
            case string:
                var rawStringVal = Convert.ToString(value, CultureInfo.InvariantCulture);
                var stringVal = rawStringVal;
                if (rawStringVal?.Length > maxAttributeValueLength)
                {
                    stringVal = rawStringVal.Substring(0, maxAttributeValueLength);
                }

                return Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.AnyValue_string_value, stringVal);
            case bool:
                return Writer.WriteBoolWithTag(ref buffer, cursor, FieldNumberConstants.AnyValue_bool_value, (bool)value);
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
            case ulong:
                return Writer.WriteInt64WithTag(ref buffer, cursor, FieldNumberConstants.AnyValue_int_value, (ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));
            case float:
            case double:
                return Writer.WriteDoubleWithTag(ref buffer, cursor, FieldNumberConstants.AnyValue_double_value, Convert.ToDouble(value, CultureInfo.InvariantCulture));
            case Array array:
                return SerializeArray(ref buffer, cursor, array, maxAttributeValueLength);
            default:
                var defaultRawStringVal = Convert.ToString(value); // , CultureInfo.InvariantCulture);
                var defaultStringVal = defaultRawStringVal;
                if (defaultRawStringVal?.Length > maxAttributeValueLength)
                {
                    defaultStringVal = defaultRawStringVal.Substring(0, maxAttributeValueLength);
                }

                return Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.AnyValue_string_value, defaultStringVal);
        }
    }
}
