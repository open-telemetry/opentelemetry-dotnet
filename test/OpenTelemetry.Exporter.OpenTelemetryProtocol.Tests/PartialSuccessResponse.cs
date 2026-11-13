// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

/// <summary>
/// Builds OTLP export responses carrying a <c>partial_success</c>, encoded by
/// hand so the tests do not depend on the reader they are testing.
/// </summary>
internal static class PartialSuccessResponse
{
    public static byte[] Create(long? rejected, string? errorMessage)
    {
        var inner = new List<byte>();

        if (rejected is { } value)
        {
            inner.Add(0x08); // field 1, varint
            WriteVarInt(inner, unchecked((ulong)value));
        }

        if (errorMessage is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(errorMessage);
            inner.Add(0x12); // field 2, length-delimited
            WriteVarInt(inner, (ulong)bytes.Length);
            inner.AddRange(bytes);
        }

        var response = new List<byte> { 0x0A }; // field 1, length-delimited
        WriteVarInt(response, (ulong)inner.Count);
        response.AddRange(inner);

        return [.. response];
    }

    public static byte[] CreateGrpcFrame(byte[] message, byte compressionFlag = 0, uint? declaredLength = null)
    {
        var length = declaredLength ?? (uint)message.Length;

        return
        [
            compressionFlag,
            (byte)(length >> 24),
            (byte)(length >> 16),
            (byte)(length >> 8),
            (byte)length,
            .. message,
        ];
    }

    private static void WriteVarInt(List<byte> buffer, ulong value)
    {
        while (value >= 0x80)
        {
            buffer.Add((byte)(value | 0x80));
            value >>= 7;
        }

        buffer.Add((byte)value);
    }
}
