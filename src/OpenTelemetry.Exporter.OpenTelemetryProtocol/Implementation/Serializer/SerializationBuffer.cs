// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

/// <summary>
/// Manages the buffer used to serialize a single OTLP export request. The buffer
/// is sized from the previous export's serialized length (to avoid resizing without
/// preserving excess capacity) and is sourced from the shared array pool via
/// <see cref="ProtobufSerializer"/>.
/// </summary>
/// <remarks>
/// On .NET Framework and .NET Standard builds the shared array pool may not pool
/// arrays larger than 1 MiB, so returning a grown export buffer larger than that
/// could discard it and force a fresh allocation on every subsequent export. Only
/// well-utilized oversized buffers are retained on this instance and reused across
/// exports (and released when the exporter shuts down); buffers the pool can reuse
/// or whose excess capacity is no longer needed are returned.
/// On modern .NET target builds the shared pool serves arbitrarily large arrays
/// and trims them under memory pressure, so each export simply rents and returns
/// without retaining anything.
/// </remarks>
internal sealed class SerializationBuffer(int initialSize)
{
#if NETFRAMEWORK || NETSTANDARD2_0_OR_GREATER
    // The largest array guaranteed to be pooled by the legacy shared array pool.
    // Arrays larger than this may not be stored on return, so retain them here to
    // avoid per-export allocation.
    private const int MaxPooledArrayLength = 1024 * 1024;
#endif

    private readonly int initialSize = initialSize;
    private int nextSize = initialSize;
#if NETFRAMEWORK || NETSTANDARD2_0_OR_GREATER
    private byte[]? retained;
#endif

    /// <summary>
    /// Obtains a buffer for the next export, sized from the previous serialized
    /// payload. The buffer contents are not cleared.
    /// </summary>
    /// <returns>A buffer that must be handed back via <see cref="Return"/>.</returns>
    public byte[] Rent()
    {
#if NETFRAMEWORK || NETSTANDARD2_0_OR_GREATER
        var buffer = this.retained;
        this.retained = null;
        return buffer ?? ProtobufSerializer.RentBuffer(this.nextSize);
#else
        return ProtobufSerializer.RentBuffer(this.nextSize);
#endif
    }

    /// <summary>
    /// Hands a buffer (possibly grown during serialization) back after successful
    /// serialization.
    /// </summary>
    /// <param name="buffer">The buffer returned by <see cref="Rent"/>.</param>
    /// <param name="serializedLength">The number of bytes written to the buffer.</param>
    public void Return(byte[] buffer, int serializedLength)
    {
        System.Diagnostics.Debug.Assert((uint)serializedLength <= (uint)buffer.Length, "Serialized length was invalid.");

        // Use the actual payload length rather than the rented capacity so a single
        // large export does not force every subsequent export to rent the same size.
        this.nextSize = Math.Max(this.initialSize, serializedLength);
#if NETFRAMEWORK || NETSTANDARD2_0_OR_GREATER
        if (buffer.Length > MaxPooledArrayLength &&
            serializedLength > MaxPooledArrayLength &&
            serializedLength > buffer.Length / 2)
        {
            // The pool may not reuse a buffer this large. Keep it only while it is
            // well utilized so a transient spike does not remain the steady size.
            this.retained = buffer;
        }
        else
        {
            // Return pool-reusable buffers and buffers whose excess capacity should
            // not be retained by this exporter.
            ProtobufSerializer.ReturnBuffer(buffer);
        }
#else
        ProtobufSerializer.ReturnBuffer(buffer);
#endif
    }

    /// <summary>
    /// Releases any oversized buffer retained for reuse. Called when the exporter shuts down.
    /// </summary>
    public void Release()
    {
        this.nextSize = this.initialSize;

#if NETFRAMEWORK || NETSTANDARD2_0_OR_GREATER
        var buffer = this.retained;
        if (buffer != null)
        {
            this.retained = null;
            ProtobufSerializer.ReturnBuffer(buffer);
        }
#endif
    }

    /// <summary>
    /// Returns a buffer after serialization failed and resets the next rental to
    /// the initial size.
    /// </summary>
    /// <param name="buffer">The buffer returned by <see cref="Rent"/>.</param>
    public void Discard(byte[] buffer)
    {
        this.nextSize = this.initialSize;
        ProtobufSerializer.ReturnBuffer(buffer);
    }
}
