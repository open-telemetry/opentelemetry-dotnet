// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpResourceSerializer
{
    private const int ReserveSizeForLength = 4;

    private static readonly string DefaultServiceName = ResourceBuilder.CreateDefault().Build().Attributes.FirstOrDefault(
        kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName).Value as string ?? "unknown_service";

    internal static int WriteResource(byte[] buffer, int writePosition, Resource? resource)
    {
        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
        };

        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpFieldNumberConstants.ResourceSpans_Resource, ProtobufWireType.LEN);
        int resourceLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        bool isServiceNamePresent = false;
        if (resource != null && resource != Resource.Empty)
        {
            if (resource.Attributes is IReadOnlyList<KeyValuePair<string, object>> resourceAttributesList)
            {
                for (int i = 0; i < resourceAttributesList.Count; i++)
                {
                    var attribute = resourceAttributesList[i];
                    if (attribute.Key == ResourceSemanticConventions.AttributeServiceName)
                    {
                        isServiceNamePresent = true;
                    }

                    otlpTagWriterState = ProcessResourceAttribute(ref otlpTagWriterState, attribute);
                }
            }
            else
            {
                foreach (var attribute in resource.Attributes)
                {
                    if (attribute.Key == ResourceSemanticConventions.AttributeServiceName)
                    {
                        isServiceNamePresent = true;
                    }

                    otlpTagWriterState = ProcessResourceAttribute(ref otlpTagWriterState, attribute);
                }
            }
        }

        if (!isServiceNamePresent)
        {
            otlpTagWriterState = ProcessResourceAttribute(ref otlpTagWriterState, new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, DefaultServiceName));
        }

        var resourceLength = otlpTagWriterState.WritePosition - (resourceLengthPosition + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, resourceLengthPosition, resourceLength);

        return otlpTagWriterState.WritePosition;
    }

    private static ProtobufOtlpTagWriter.OtlpTagWriterState ProcessResourceAttribute(ref ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState, KeyValuePair<string, object> attribute)
    {
        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpFieldNumberConstants.Resource_Attributes, ProtobufWireType.LEN);
        int resourceAttributesLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, attribute.Key, attribute.Value);

        var resourceAttributesLength = otlpTagWriterState.WritePosition - (resourceAttributesLengthPosition + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, resourceAttributesLengthPosition, resourceAttributesLength);
        return otlpTagWriterState;
    }
}
