// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Text;
using ProcessContextMessage = OpenTelemetry.Proto.ProcessContext.V1Development.ProcessContext;

namespace OpenTelemetry.Extensions.ProcessContext.Tests;

/// <summary>
/// Reads a header the way an external reader would, from raw memory.
/// </summary>
internal readonly record struct HeaderReader(string Signature, uint Version, uint PayloadSize, ulong PublishedAt, nint Payload)
{
    public static HeaderReader Read(nint header)
    {
        var signature = new byte[8];
        Marshal.Copy(header, signature, 0, 8);
        return new HeaderReader(
            Encoding.ASCII.GetString(signature),
            unchecked((uint)Marshal.ReadInt32(header, 8)),
            unchecked((uint)Marshal.ReadInt32(header, 12)),
            unchecked((ulong)Marshal.ReadInt64(header, 16)),
            (nint)Marshal.ReadInt64(header, 24));
    }

    public byte[] ReadPayloadBytes()
    {
        var bytes = new byte[this.PayloadSize];
        Marshal.Copy(this.Payload, bytes, 0, bytes.Length);
        return bytes;
    }

    public ProcessContextMessage DecodePayload()
        => ProcessContextMessage.Parser.ParseFrom(this.ReadPayloadBytes());
}
