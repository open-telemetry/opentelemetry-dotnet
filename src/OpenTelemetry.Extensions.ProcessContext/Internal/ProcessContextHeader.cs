// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

/// <summary>
/// The 32 byte process context header, stored at offset zero of the mapping
/// in native byte order with no padding.
/// </summary>
/// <remarks>
/// All access goes through <see cref="Marshal"/> so that no unsafe code is
/// needed. The 64-bit fields sit at 8 byte aligned offsets of a page aligned
/// mapping, so each of their writes is a single aligned store, and <see
/// cref="Interlocked.MemoryBarrier"/> is a full (sequentially consistent)
/// fence, matching <c>atomic_thread_fence(memory_order_seq_cst)</c>.
/// </remarks>
internal static class ProcessContextHeader
{
    internal const int Size = 32;
    internal const int SignatureOffset = 0;
    internal const int SignatureLength = 8;
    internal const int VersionOffset = 8;
    internal const int PayloadSizeOffset = 12;
    internal const int PublishedAtOffset = 16;
    internal const int PayloadPointerOffset = 24;

    /// <summary>
    /// The format version from the specification. Readers ignore any other
    /// value (1 is reserved for development).
    /// </summary>
    internal const int Version = 2;

    internal static readonly byte[] Signature = [(byte)'O', (byte)'T', (byte)'E', (byte)'L', (byte)'_', (byte)'C', (byte)'T', (byte)'X'];

    /// <summary>
    /// Publication protocol steps 7 to 9: writes every field, then a full
    /// fence, then the timestamp which makes the header valid.
    /// </summary>
    /// <param name="header">Address of the header (start of the mapping).</param>
    /// <param name="payload">Address of the encoded payload.</param>
    /// <param name="payloadSize">Size of the encoded payload in bytes.</param>
    /// <param name="publishedAtNanoseconds">Non-zero <c>CLOCK_BOOTTIME</c> timestamp.</param>
    internal static void Publish(nint header, nint payload, int payloadSize, ulong publishedAtNanoseconds)
    {
        Marshal.Copy(Signature, 0, header + SignatureOffset, SignatureLength);
        Marshal.WriteInt32(header, VersionOffset, Version);
        Marshal.WriteInt32(header, PayloadSizeOffset, payloadSize);
        Marshal.WriteInt64(header, PayloadPointerOffset, payload);

        Interlocked.MemoryBarrier();

        Marshal.WriteInt64(header, PublishedAtOffset, unchecked((long)publishedAtNanoseconds));
    }

    /// <summary>
    /// Update protocol steps 2 to 6: zeroes the timestamp, fences, swaps the
    /// payload pointer and size, fences, then writes the new timestamp.
    /// </summary>
    /// <param name="header">Address of the header (start of the mapping).</param>
    /// <param name="payload">Address of the new encoded payload, already fully written.</param>
    /// <param name="payloadSize">Size of the new encoded payload in bytes.</param>
    /// <param name="publishedAtNanoseconds">New timestamp, greater than the previous one.</param>
    /// <param name="onMutating">Test hook invoked while the timestamp is zero.</param>
    internal static void Update(nint header, nint payload, int payloadSize, ulong publishedAtNanoseconds, Action? onMutating = null)
    {
        Marshal.WriteInt64(header, PublishedAtOffset, 0);

        Interlocked.MemoryBarrier();

        onMutating?.Invoke();

        Marshal.WriteInt32(header, PayloadSizeOffset, payloadSize);
        Marshal.WriteInt64(header, PayloadPointerOffset, payload);

        Interlocked.MemoryBarrier();

        Marshal.WriteInt64(header, PublishedAtOffset, unchecked((long)publishedAtNanoseconds));
    }

    /// <summary>
    /// Zeroes the timestamp so that readers stop trusting the header before
    /// the mapping is released.
    /// </summary>
    /// <param name="header">Address of the header (start of the mapping).</param>
    internal static void Invalidate(nint header)
    {
        Marshal.WriteInt64(header, PublishedAtOffset, 0);

        Interlocked.MemoryBarrier();
    }

    internal static ulong ReadPublishedAt(nint header)
        => unchecked((ulong)Marshal.ReadInt64(header, PublishedAtOffset));
}
