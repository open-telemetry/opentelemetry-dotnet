// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Globalization;
using System.Text;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

internal static class CommonTypesSizeCalculator
{
    internal static int ComputeStringWithTagSize(int fieldNumber, string value)
    {
        int size = 0;
        size += WireTypesSizeCalculator.ComputeTagSize(fieldNumber);
        var stringLength = Encoding.UTF8.GetByteCount(value);
        size += WireTypesSizeCalculator.ComputeLengthSize(stringLength);
        size += stringLength;

        return size;
    }

    internal static int ComputeSizeWithTagAndLengthPrefix(int fieldNumber, int numberOfbytes)
    {
        int size = 0;
        size += WireTypesSizeCalculator.ComputeTagSize(fieldNumber);
        size += WireTypesSizeCalculator.ComputeLengthSize(numberOfbytes); // length prefix for key value pair.
        size += numberOfbytes;

        return size;
    }

    internal static int ComputeInstrumentationScopeSize(string scopeName, string? scopeVersion)
    {
        int size = 0;

        size += ComputeStringWithTagSize(FieldNumberConstants.InstrumentationScope_name, scopeName);

        if (scopeVersion != null)
        {
            size += ComputeStringWithTagSize(FieldNumberConstants.InstrumentationScope_version, scopeVersion);
        }

        return size;
    }

    internal static int ComputeKeyValuePairSize(KeyValuePair<string, object?> tag, int maxAttributeValueLength)
    {
        int size = 0;
        size += ComputeStringWithTagSize(FieldNumberConstants.KeyValue_key, tag.Key);

        var anyValueSize = ComputeAnyValueSize(tag.Value, maxAttributeValueLength);
        size += ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.KeyValue_value, anyValueSize);

        return size;
    }

    internal static int ComputeAnyValueSize(object? value, int maxAttributeValueLength)
    {
        if (value == null)
        {
            return 0;
        }

        switch (value)
        {
            case char:
                return ComputeStringWithTagSize(FieldNumberConstants.AnyValue_string_value, Convert.ToString(value, CultureInfo.InvariantCulture)!);
            case string:
                var rawStringVal = Convert.ToString(value, CultureInfo.InvariantCulture);
                var stringVal = rawStringVal;
                if (rawStringVal?.Length > maxAttributeValueLength)
                {
                    stringVal = rawStringVal.Substring(0, maxAttributeValueLength);
                }

                return ComputeStringWithTagSize(FieldNumberConstants.AnyValue_string_value, stringVal!);
            case bool:
                return 1 + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_bool_value);
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
            case ulong:
                return WireTypesSizeCalculator.ComputeRawVarint64Size((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)) + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_int_value);
            case float:
            case double:
                return 8 + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_double_value);
            case Array array:
                var arraySize = ComputeArrayValueSize(array, maxAttributeValueLength);
                return WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_array_value) + WireTypesSizeCalculator.ComputeLengthSize(arraySize) + arraySize;
            default:
                var defaultRawStringVal = Convert.ToString(value); // , CultureInfo.InvariantCulture);
                var defaultStringVal = defaultRawStringVal;
                if (defaultRawStringVal?.Length > maxAttributeValueLength)
                {
                    defaultStringVal = defaultRawStringVal.Substring(0, maxAttributeValueLength);
                }

                return ComputeStringWithTagSize(FieldNumberConstants.AnyValue_string_value, defaultStringVal!);
        }
    }

    internal static int ComputeArrayValueSize(Array array, int maxAttributeValueLength)
    {
        int size = 0;
        foreach (var value in array)
        {
            var anyValueSize = ComputeAnyValueSize(value, maxAttributeValueLength);
            size += ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.ArrayValue_Value, anyValueSize);
        }

        return size;
    }

    internal static int ComputeResourceSize(Resource resource, int maxAttributeValueLength)
    {
        int size = 0;
        if (resource != null && resource != Resource.Empty)
        {
            foreach (var attribute in resource.Attributes)
            {
                var keyValueSize = ComputeKeyValuePairSize(attribute!, maxAttributeValueLength);
                size += ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Resource_attributes, keyValueSize);
            }
        }

        return size;
    }
}
