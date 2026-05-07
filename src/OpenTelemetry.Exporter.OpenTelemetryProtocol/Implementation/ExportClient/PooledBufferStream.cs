// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Buffers;
using System.Diagnostics;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>
/// A growable Stream backed by buffers rented from an ArrayPool{byte}.
/// Returns rented buffers to the pool when disposed (unless detached).
/// </summary>
internal sealed class PooledBufferStream : Stream
{
    private readonly ArrayPool<byte> pool;

    private byte[] buffer;
    private bool disposed;
    private int length;
    private int position;

    public PooledBufferStream(int initialCapacity = 0, ArrayPool<byte>? pool = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity, nameof(initialCapacity));

        this.pool = pool ?? ArrayPool<byte>.Shared;

        // Always rent at least 1 byte so we can hand out a stable array instance.
        this.buffer = this.pool.Rent(Math.Max(1, initialCapacity));
        this.length = 0;
        this.position = 0;
    }

    /// <inheritdoc/>
    public override bool CanRead => !this.disposed;

    /// <inheritdoc/>
    public override bool CanSeek => !this.disposed;

    /// <inheritdoc/>
    public override bool CanWrite => !this.disposed;

    /// <inheritdoc/>
    public override long Length
    {
        get
        {
            this.ThrowIfDisposed();
            return this.length;
        }
    }

    /// <inheritdoc/>
    public override long Position
    {
        get
        {
            this.ThrowIfDisposed();
            return this.position;
        }

        set
        {
            this.ThrowIfDisposed();

            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(value));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, int.MaxValue, nameof(value));

            this.position = (int)value;
        }
    }

    /// <inheritdoc/>
    public override void Flush()
        => this.ThrowIfDisposed(); // Nothing to flush

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateReadWriteArgs(buffer, offset, count);

        this.ThrowIfDisposed();

        int available = this.length - this.position;
        if (available <= 0)
        {
            return 0;
        }

        int toCopy = Math.Min(available, count);
        Buffer.BlockCopy(this.buffer, this.position, buffer, offset, toCopy);
        this.position += toCopy;

        return toCopy;
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> destination)
    {
        this.ThrowIfDisposed();

        int available = this.length - this.position;
        if (available <= 0)
        {
            return 0;
        }

        int toCopy = Math.Min(available, destination.Length);
        this.buffer.AsSpan(this.position, toCopy).CopyTo(destination);
        this.position += toCopy;

        return toCopy;
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled<int>(cancellationToken);
        }

        try
        {
            int bytesRead = this.Read(buffer.Span);
            return ValueTask.FromResult(bytesRead);
        }
        catch (Exception ex)
        {
            return ValueTask.FromException<int>(ex);
        }
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        this.ThrowIfDisposed();

        long newOffset = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => this.position + offset,
            SeekOrigin.End => this.length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "Invalid seek origin."),
        };

        if (newOffset < 0 || newOffset > int.MaxValue)
        {
            throw new IOException("Attempted to seek outside the bounds of the stream.");
        }

        this.position = (int)newOffset;
        return this.position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        this.ThrowIfDisposed();

        ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(value));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, int.MaxValue, nameof(value));

        int newLength = (int)value;
        this.EnsureCapacity(newLength);

        // If we grew length, zero the gap to preserve typical MemoryStream behavior.
        if (newLength > this.length)
        {
            this.buffer.AsSpan(this.length, newLength - this.length).Clear();
        }

        this.length = newLength;

        if (this.position > this.length)
        {
            this.position = this.length;
        }
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateReadWriteArgs(buffer, offset, count);

        this.ThrowIfDisposed();
        this.EnsureWriteCapacity(count);

        Buffer.BlockCopy(buffer, offset, this.buffer, this.position, count);
        this.position += count;

        if (this.position > this.length)
        {
            this.length = this.position;
        }
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> source)
    {
        this.ThrowIfDisposed();

        this.EnsureWriteCapacity(source.Length);

        source.CopyTo(this.buffer.AsSpan(this.position));
        this.position += source.Length;

        if (this.position > this.length)
        {
            this.length = this.position;
        }
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromCanceled(cancellationToken);
        }

        try
        {
            this.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
        catch (Exception ex)
        {
            return ValueTask.FromException(ex);
        }
    }

    /// <inheritdoc/>
    public override void WriteByte(byte value)
    {
        this.ThrowIfDisposed();

        this.EnsureWriteCapacity(1);

        this.buffer[this.position] = value;
        this.position++;

        if (this.position > this.length)
        {
            this.length = this.position;
        }
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
        this.ThrowIfDisposed();
        return this.position >= this.length ? -1 : this.buffer[this.position++];
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            base.Dispose(disposing);
            return;
        }

        this.disposed = true;

        if (disposing)
        {
            var rented = this.buffer;
            this.buffer = [];
            this.length = 0;
            this.position = 0;

            if (rented is { Length: > 0 })
            {
                this.pool.Return(rented, clearArray: true);
            }
        }

        base.Dispose(disposing);
    }

    private static int ComputeNewCapacity(int minCapacity, int currentCapacity)
    {
        // Growth heuristic: double, with a small starting point.
        int newCapacity = currentCapacity switch
        {
            <= 0 => 256,
            < 1024 * 1024 => currentCapacity * 2,
            _ => currentCapacity + (currentCapacity / 2), // 1.5x after 1MB
        };

        if (newCapacity < minCapacity)
        {
            newCapacity = minCapacity;
        }

        // Avoid overflow edge-cases.
        if (newCapacity < 0)
        {
            newCapacity = minCapacity;
        }

        return newCapacity;
    }

    private static void ValidateReadWriteArgs(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset, (uint)buffer.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)(buffer.Length - offset));
    }

    private void EnsureCapacity(int capacity)
    {
        this.ThrowIfDisposed();

        ArgumentOutOfRangeException.ThrowIfNegative(capacity, nameof(capacity));

        if (this.buffer.Length < capacity)
        {
            this.Grow(capacity);
        }
    }

    private void EnsureWriteCapacity(int additionalCount)
    {
        Debug.Assert(additionalCount >= 0, $"{nameof(additionalCount)} is negative.");

        long required = (long)this.position + additionalCount;
        if (required > int.MaxValue)
        {
            throw new IOException($"The stream's buffer cannot be greater than {int.MaxValue} bytes in length.");
        }

        if (required > this.buffer.Length)
        {
            this.Grow((int)required);
        }

        // If writing beyond current length, ensure the gap is zeroed (MemoryStream-like).
        // For example: Position=10, Length=0, Write 1 byte => bytes [0..9] become 0.
        if (this.position > this.length)
        {
            this.buffer.AsSpan(this.length, this.position - this.length).Clear();
        }
    }

    private void Grow(int minCapacity)
    {
        this.ThrowIfDisposed();

        byte[] previous = this.buffer;

        int newCapacity = ComputeNewCapacity(minCapacity, previous.Length);

        byte[] replacement = this.pool.Rent(newCapacity);

        if (this.length != 0)
        {
            Buffer.BlockCopy(previous, 0, replacement, 0, this.length);
        }

        this.buffer = replacement;
        this.pool.Return(previous, clearArray: true);
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(this.disposed, this);
}

#endif
