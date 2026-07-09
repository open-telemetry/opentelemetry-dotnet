// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

/// <summary>
/// Manages the buffer used to serialize a single OTLP export request. The buffer
/// is sized from the previous export (to avoid resizing) and is sourced from the
/// shared array pool via <see cref="ProtobufSerializer"/>.
/// </summary>
/// <remarks>
/// On .NET Framework and netstandard2.0 the shared array pool does not pool arrays
/// larger than 1 MiB, so returning a grown export buffer larger than that would
/// discard it and force a fresh allocation on every subsequent export. Only those
/// oversized buffers are retained on this instance and reused across exports (and
/// released when the exporter shuts down); buffers the pool can reuse (1 MiB or
/// smaller) are returned so nothing is retained per exporter for the common case.
/// On modern runtimes the shared pool serves arbitrarily large arrays and trims
/// them under memory pressure, so each export simply rents and returns without
/// retaining anything.
/// </remarks>
internal sealed class SerializationBuffer(int initialSize)
{
#if NETFRAMEWORK || NETSTANDARD2_0
    // The largest array the .NET Framework / netstandard2.0 shared array pool will
    // pool and hand back out. Arrays larger than this are neither pooled on rent nor
    // stored on return, so they must be retained here to avoid per-export allocation.
    private const int MaxPooledArrayLength = 1024 * 1024;
#endif

    private int nextSize = initialSize;
#if NETFRAMEWORK || NETSTANDARD2_0
    private byte[]? retained;
#endif

    /// <summary>
    /// Obtains a buffer for the next export, at least as large as the previous
    /// export's final buffer. The buffer contents are not cleared.
    /// </summary>
    /// <returns>A buffer that must be handed back via <see cref="Return"/>.</returns>
    public byte[] Rent()
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        var buffer = this.retained;
        this.retained = null;
        return buffer ?? ProtobufSerializer.RentBuffer(this.nextSize);
#else
        return ProtobufSerializer.RentBuffer(this.nextSize);
#endif
    }

    /// <summary>
    /// Hands a buffer (possibly grown during serialization) back once the export
    /// has completed.
    /// </summary>
    /// <param name="buffer">The buffer returned by <see cref="Rent"/>.</param>
    public void Return(byte[] buffer)
    {
        // Remember the (possibly grown) capacity so the next export starts from a
        // buffer large enough to avoid resizing.
        this.nextSize = buffer.Length;
#if NETFRAMEWORK || NETSTANDARD2_0
        if (buffer.Length > MaxPooledArrayLength)
        {
            // The pool cannot reuse a buffer this large, so keep it for the next
            // export instead of letting the pool discard it.
            this.retained = buffer;
        }
        else
        {
            // The pool can reuse a buffer this size, so return it rather than
            // retaining it per exporter.
            ProtobufSerializer.ReturnBuffer(buffer);
        }
#else
        ProtobufSerializer.ReturnBuffer(buffer);
#endif
    }

#if NETFRAMEWORK || NETSTANDARD2_0
    /// <summary>
    /// Releases any oversized buffer retained for reuse. Called when the exporter shuts down.
    /// </summary>
    public void Release()
    {
        var buffer = this.retained;
        if (buffer != null)
        {
            this.retained = null;
            ProtobufSerializer.ReturnBuffer(buffer);
        }
    }
#endif
}
