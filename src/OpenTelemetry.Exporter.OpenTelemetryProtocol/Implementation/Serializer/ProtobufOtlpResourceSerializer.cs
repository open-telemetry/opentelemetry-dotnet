// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpResourceSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int InitialScratchSize = 8192;

    private static readonly ConcurrentDictionary<Resource, byte[]> CachedResourceBytes = new();

    private static ReadOnlySpan<byte> EmptyResourceBytes => [0x0A, 0x80, 0x80, 0x80, 0x00];

    internal static int WriteResource(byte[] buffer, int writePosition, Resource? resource)
    {
        if (resource == null || resource == Resource.Empty)
        {
            EmptyResourceBytes.CopyTo(buffer.AsSpan(writePosition));
            return writePosition + EmptyResourceBytes.Length;
        }

        var cached = CachedResourceBytes.GetOrAdd(resource, static r => SerializeResourceToBytes(r));
        Buffer.BlockCopy(cached, 0, buffer, writePosition, cached.Length);
        return writePosition + cached.Length;
    }

    private static byte[] SerializeResourceToBytes(Resource resource)
    {
        var pool = ArrayPool<byte>.Shared;

        var scratch = pool.Rent(InitialScratchSize);
        try
        {
            while (true)
            {
                try
                {
                    var length = WriteResourceCore(scratch, 0, resource);
                    return scratch.AsSpan(0, length).ToArray();
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
                {
                    pool.Return(scratch);
                    scratch = pool.Rent(scratch.Length * 2);
                }
            }
        }
        finally
        {
            pool.Return(scratch);
        }
    }

    private static int WriteResourceCore(byte[] buffer, int writePosition, Resource resource)
    {
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
        };

        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Resource, ProtobufWireType.LEN);
        var resourceLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        if (resource.Attributes is IReadOnlyList<KeyValuePair<string, object>> resourceAttributesList)
        {
            for (var i = 0; i < resourceAttributesList.Count; i++)
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

        var resourceLength = otlpTagWriterState.WritePosition - (resourceLengthPosition + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, resourceLengthPosition, resourceLength);

        return otlpTagWriterState.WritePosition;
    }

    private static void ProcessResourceAttribute(ref ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState, KeyValuePair<string, object> attribute)
    {
        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Resource_Attributes, ProtobufWireType.LEN);
        var resourceAttributesLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, attribute.Key, attribute.Value);

        var resourceAttributesLength = otlpTagWriterState.WritePosition - (resourceAttributesLengthPosition + ReserveSizeForLength);
        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, resourceAttributesLengthPosition, resourceAttributesLength);
    }
}
