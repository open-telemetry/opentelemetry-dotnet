// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.CompilerServices;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpResourceSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int InitialBufferSize = 2048;

#if NETFRAMEWORK || NETSTANDARD2_0
    private static readonly ConditionalWeakTable<Resource, byte[]> CachedResourceBytes = new();
#else
    private static readonly ConditionalWeakTable<Resource, byte[]> CachedResourceBytes = [];
#endif

    private static ReadOnlySpan<byte> EmptyResourceBytes => [0x0A, 0x80, 0x80, 0x80, 0x00];

    internal static int WriteResource(byte[] buffer, int writePosition, Resource? resource)
    {
        if (resource == null || resource == Resource.Empty)
        {
            EmptyResourceBytes.CopyTo(buffer.AsSpan(writePosition));
            return writePosition + EmptyResourceBytes.Length;
        }

        var cached = CachedResourceBytes.GetOrAdd(resource, SerializeResourceToBytes);
        Buffer.BlockCopy(cached, 0, buffer, writePosition, cached.Length);
        return writePosition + cached.Length;
    }

    internal static byte[] SerializeResourceToBytes(Resource resource, ArrayPool<byte> pool, int maxBufferSize)
    {
        var buffer = pool.Rent(Math.Min(InitialBufferSize, maxBufferSize));

        try
        {
            while (true)
            {
                try
                {
                    var length = WriteResourceCore(buffer, 0, resource);
                    if (length > maxBufferSize)
                    {
                        throw new InvalidOperationException("The serialized resource exceeds the maximum buffer size.");
                    }

                    return buffer.AsSpan(0, length).ToArray();
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
                {
                    if (buffer.Length >= maxBufferSize)
                    {
                        // A buffer-full exception would make the outer serializer
                        // grow its buffer and repeat this failed serialization.
                        throw new InvalidOperationException(
                            "The serialized resource exceeds the maximum buffer size.",
                            ex);
                    }

                    var smallerBuffer = buffer;
                    buffer = pool.Rent((int)Math.Min((long)buffer.Length * 2, maxBufferSize));
                    ProtobufSerializer.ReturnBuffer(pool, smallerBuffer);
                }
            }
        }
        finally
        {
            ProtobufSerializer.ReturnBuffer(pool, buffer);
        }
    }

    private static byte[] SerializeResourceToBytes(Resource resource) =>
        SerializeResourceToBytes(resource, ArrayPool<byte>.Shared, ProtobufSerializer.MaxBufferSize);

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

    private static void ProcessResourceAttribute(ref ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState, KeyValuePair<string, object> attribute) =>
        _ = ProtobufOtlpTagWriter.WriteKeyValue(
            ref otlpTagWriterState,
            ProtobufOtlpTraceFieldNumberConstants.Resource_Attributes,
            attribute.Key,
            attribute.Value);
}
