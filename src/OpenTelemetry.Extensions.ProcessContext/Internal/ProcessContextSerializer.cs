// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// Encodes the <c>opentelemetry.proto.processcontext.v1development.ProcessContext</c>
/// message:
/// <code>
/// message ProcessContext {
///   opentelemetry.proto.resource.v1.Resource resource = 1;
///   repeated opentelemetry.proto.common.v1.KeyValue attributes = 2;
/// }
/// </code>
/// </summary>
internal static class ProcessContextSerializer
{
    // ProcessContext
    internal const int ProcessContextResource = 1;
    internal const int ProcessContextAttributes = 2;

    // Resource
    internal const int ResourceAttributes = 1;

    // KeyValue
    internal const int KeyValueKey = 1;
    internal const int KeyValueValue = 2;

    // AnyValue
    internal const int AnyValueStringValue = 1;
    internal const int AnyValueBoolValue = 2;
    internal const int AnyValueIntValue = 3;
    internal const int AnyValueDoubleValue = 4;
    internal const int AnyValueArrayValue = 5;
    internal const int AnyValueBytesValue = 7;

    // ArrayValue
    internal const int ArrayValueValues = 1;

    /// <summary>
    /// Serializes a <c>ProcessContext</c> payload.
    /// </summary>
    /// <param name="resource">The resource to publish.</param>
    /// <param name="attributes">Additional attributes for external readers, if any.</param>
    /// <returns>The encoded payload. It is never empty: the <c>resource</c> field is always written.</returns>
    internal static byte[] Serialize(Resource resource, IEnumerable<KeyValuePair<string, object?>>? attributes)
    {
        var writer = new ProtobufWriter();

        // The resource field is written even when it has no attributes, so
        // that the payload is never zero bytes long (readers reject that).
        var resourcePosition = writer.BeginMessage(ProcessContextResource);
        WriteAttributes(writer, ResourceAttributes, resource.Attributes.Select(a => new KeyValuePair<string, object?>(a.Key, a.Value)));
        writer.EndMessage(resourcePosition);

        if (attributes != null)
        {
            WriteAttributes(writer, ProcessContextAttributes, attributes);
        }

        return writer.ToArray();
    }

    private static void WriteAttributes(ProtobufWriter writer, int fieldNumber, IEnumerable<KeyValuePair<string, object?>> attributes)
    {
        // Keys MUST be unique. Keep the last value for a repeated key, which
        // is the same precedence Resource.Merge uses.
        var unique = new List<KeyValuePair<string, object?>>();
        var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var attribute in attributes)
        {
            if (string.IsNullOrEmpty(attribute.Key))
            {
                continue;
            }

            if (indexByKey.TryGetValue(attribute.Key, out var index))
            {
                unique[index] = attribute;
            }
            else
            {
                indexByKey[attribute.Key] = unique.Count;
                unique.Add(attribute);
            }
        }

        foreach (var attribute in unique)
        {
            var startPosition = writer.Position;
            var keyValuePosition = writer.BeginMessage(fieldNumber);
            writer.WriteStringField(KeyValueKey, attribute.Key);

            if (!TryWriteAnyValue(writer, KeyValueValue, attribute.Value))
            {
                ProcessContextEventSource.Log.UnsupportedAttributeType(attribute.Key, attribute.Value?.GetType().FullName ?? "null");
                writer.Rewind(startPosition);
                continue;
            }

            writer.EndMessage(keyValuePosition);
        }
    }

    private static bool TryWriteAnyValue(ProtobufWriter writer, int fieldNumber, object? value)
    {
        switch (value)
        {
            case string s:
                {
                    var p = writer.BeginMessage(fieldNumber);
                    writer.WriteStringField(AnyValueStringValue, s);
                    writer.EndMessage(p);
                    return true;
                }

            case bool b:
                {
                    var p = writer.BeginMessage(fieldNumber);
                    writer.WriteTag(AnyValueBoolValue, ProtobufWireType.VARINT);
                    writer.WriteVarInt(b ? 1UL : 0UL);
                    writer.EndMessage(p);
                    return true;
                }

            case long or int or short or sbyte or uint or ushort or byte:
                {
                    var p = writer.BeginMessage(fieldNumber);
                    writer.WriteTag(AnyValueIntValue, ProtobufWireType.VARINT);
                    writer.WriteVarInt(unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)));
                    writer.EndMessage(p);
                    return true;
                }

            case double or float:
                {
                    var p = writer.BeginMessage(fieldNumber);
                    writer.WriteTag(AnyValueDoubleValue, ProtobufWireType.I64);
                    writer.WriteFixed64(unchecked((ulong)BitConverter.DoubleToInt64Bits(Convert.ToDouble(value, CultureInfo.InvariantCulture))));
                    writer.EndMessage(p);
                    return true;
                }

            case byte[] bytes:
                {
                    var p = writer.BeginMessage(fieldNumber);
                    writer.WriteBytesField(AnyValueBytesValue, bytes);
                    writer.EndMessage(p);
                    return true;
                }

            case Array array:
                {
                    // Check every element first, so that an unsupported element
                    // drops the whole attribute rather than writing part of it.
                    foreach (var element in array)
                    {
                        if (element is not (string or bool or long or int or short or sbyte or uint or ushort or byte or double or float))
                        {
                            return false;
                        }
                    }

                    var p = writer.BeginMessage(fieldNumber);
                    var arrayPosition = writer.BeginMessage(AnyValueArrayValue);
                    foreach (var element in array)
                    {
                        TryWriteAnyValue(writer, ArrayValueValues, element);
                    }

                    writer.EndMessage(arrayPosition);
                    writer.EndMessage(p);
                    return true;
                }

            default:
                return false;
        }
    }
}
