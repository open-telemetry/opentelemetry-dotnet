// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ResourceProtoSerializer
{
    private const int ReserveSizeForLength = 4;

    internal static int WriteResource(byte[] buffer, int writePosition, Resource? resource)
    {
        OtlpProtoTagWriter.OtlpTagWriterState otlpTagWriterState = new OtlpProtoTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
        };

        if (resource != null && resource != Resource.Empty)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, FieldNumberConstants.ResourceSpans_Resource, ProtobufWireType.LEN);
            int resourceLengthPosition = otlpTagWriterState.WritePosition;
            otlpTagWriterState.WritePosition += ReserveSizeForLength;

            foreach (KeyValuePair<string, object> attribute in resource.Attributes)
            {
                otlpTagWriterState = ProcessResourceAttribute(otlpTagWriterState, attribute);
            }

            if (!resource.Attributes.Any(kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName))
            {
                var serviceName = (string)ResourceBuilder.CreateDefault().Build().Attributes.FirstOrDefault(
                    kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName).Value;

                otlpTagWriterState = ProcessResourceAttribute(otlpTagWriterState, new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, serviceName));
            }

            var resourceLength = otlpTagWriterState.WritePosition - (resourceLengthPosition + ReserveSizeForLength);
            ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, resourceLengthPosition, resourceLength);
        }

        return otlpTagWriterState.WritePosition;
    }

    private static OtlpProtoTagWriter.OtlpTagWriterState ProcessResourceAttribute(OtlpProtoTagWriter.OtlpTagWriterState otlpTagWriterState, KeyValuePair<string, object> attribute)
    {
        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, FieldNumberConstants.Resource_Attributes, ProtobufWireType.LEN);
        int resourceAttributesLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        OtlpProtoTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, attribute.Key, attribute.Value);

        var resourceAttributesLength = otlpTagWriterState.WritePosition - (resourceAttributesLengthPosition + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, resourceAttributesLengthPosition, resourceAttributesLength);
        return otlpTagWriterState;
    }
}
