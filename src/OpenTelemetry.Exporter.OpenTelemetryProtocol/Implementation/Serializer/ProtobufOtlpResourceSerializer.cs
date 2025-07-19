// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpResourceSerializer
{
    private const int ReserveSizeForLength = 4;

    internal static int WriteResource(byte[] buffer, int writePosition, Resource? resource)
    {
        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
        };

        otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Resource, ProtobufWireType.LEN);
        int resourceLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        if (resource != null && resource != Resource.Empty)
        {
            if (resource.Attributes is IReadOnlyList<KeyValuePair<string, object>> resourceAttributesList)
            {
                for (int i = 0; i < resourceAttributesList.Count; i++)
                {
                    ProcessResourceAttribute(ref otlpTagWriterState, resourceAttributesList[i]);
                }
            }
            else
            {
                foreach (var attribute in resource.Attributes)
                {
                    ProcessResourceAttribute(ref otlpTagWriterState, attribute);
                }
            }
        }

        var resourceLength = otlpTagWriterState.WritePosition - (resourceLengthPosition + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer.AsSpan(resourceLengthPosition), resourceLength);

        return otlpTagWriterState.WritePosition;
    }

    private static void ProcessResourceAttribute(ref ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState, KeyValuePair<string, object> attribute)
    {
        otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpTraceFieldNumberConstants.Resource_Attributes, ProtobufWireType.LEN);
        int resourceAttributesLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, attribute.Key, attribute.Value);

        var resourceAttributesLength = otlpTagWriterState.WritePosition - (resourceAttributesLengthPosition + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer.AsSpan(resourceAttributesLengthPosition), resourceAttributesLength);
    }
}
