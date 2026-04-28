// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET

using System.Buffers;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.ExportClient;

public class PooledBufferStreamTests
{
    [Fact]
    public void ReadWriteSeekAndFlush_BehaveLikeMemoryStream()
    {
        using var stream = new PooledBufferStream(initialCapacity: 1);

        stream.WriteByte(1);
        stream.Write([2, 3, 4]);
        stream.Flush();

        Assert.Equal(4, stream.Length);
        Assert.Equal(4, stream.Position);
        Assert.Equal(2, stream.Seek(-2, SeekOrigin.Current));
        Assert.Equal(4, stream.Seek(0, SeekOrigin.End));

        stream.Position = 0;

        Span<byte> firstRead = stackalloc byte[2];
        Assert.Equal(2, stream.Read(firstRead));
        Assert.Equal(new byte[] { 1, 2 }, firstRead.ToArray());

        var remainder = new byte[2];
        Assert.Equal(2, stream.Read(remainder, 0, remainder.Length));
        Assert.Equal(new byte[] { 3, 4 }, remainder);
        Assert.Equal(-1, stream.ReadByte());
        Assert.Equal(0, stream.Read(remainder, 0, remainder.Length));
        Assert.Equal(0, stream.Read([]));
    }

    [Fact]
    public void SparseWritesAndSetLength_ZeroIntermediateBytes_AndClampPosition()
    {
        using var stream = new PooledBufferStream(initialCapacity: 1);

        stream.Write([1, 2]);
        stream.Position = 5;
        stream.WriteByte(9);

        stream.SetLength(8);

        stream.Position = 0;

        var expandedBuffer = new byte[8];
        Assert.Equal(8, stream.Read(expandedBuffer, 0, expandedBuffer.Length));
        Assert.Equal(new byte[] { 1, 2, 0, 0, 0, 9, 0, 0 }, expandedBuffer);

        stream.Position = 8;
        stream.SetLength(3);

        Assert.Equal(3, stream.Length);
        Assert.Equal(3, stream.Position);

        stream.Position = 0;

        var buffer = new byte[3];
        Assert.Equal(3, stream.Read(buffer, 0, buffer.Length));
        Assert.Equal(new byte[] { 1, 2, 0 }, buffer);
    }

    [Fact]
    public async Task AsyncReadWrite_HonorCancellationAndDisposedState()
    {
        var stream = new PooledBufferStream();

        await stream.WriteAsync(new byte[] { 10, 20, 30 });
        stream.Position = 0;

        var readBuffer = new byte[3];
        Assert.Equal(3, await stream.ReadAsync(readBuffer));
        Assert.Equal(new byte[] { 10, 20, 30 }, readBuffer);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream.ReadAsync(new byte[1], cancellationTokenSource.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream.WriteAsync(new byte[1], cancellationTokenSource.Token).AsTask());

        await stream.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.ReadAsync(new byte[1]).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.WriteAsync(new byte[1]).AsTask());
    }

    [Fact]
    public void SeekAndPositionValidation_ThrowsForInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PooledBufferStream(-1));

        using var stream = new PooledBufferStream();

        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Position = (long)int.MaxValue + 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SetLength(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.SetLength((long)int.MaxValue + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Seek(0, (SeekOrigin)99));
        Assert.Throws<IOException>(() => stream.Seek(-1, SeekOrigin.Begin));
        Assert.Throws<IOException>(() => stream.Seek((long)int.MaxValue + 1, SeekOrigin.Begin));
    }

    [Fact]
    public void ReadAndWriteValidation_ThrowsForInvalidArguments()
    {
        using var stream = new PooledBufferStream();

        Assert.Throws<ArgumentNullException>(() => stream.Read(null!, 0, 0));
        Assert.Throws<ArgumentNullException>(() => stream.Write(null!, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read([1], 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write([1], 2, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read([1], 0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write([1], 0, 2));
    }

    [Fact]
    public void WriteByte_WhenPositionWouldOverflow_Throws()
    {
        using var stream = new PooledBufferStream();

        stream.Position = int.MaxValue;

        var exception = Assert.Throws<IOException>(() => stream.WriteByte(1));
        Assert.Contains(int.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_ReturnsRentedBuffersAndMarksStreamUnavailable()
    {
        var pool = new TrackingArrayPool();
        var stream = new PooledBufferStream(initialCapacity: 1, pool);

        stream.Write([1, 2]);

        Assert.Equal(2, pool.Rented.Count);
        Assert.Single(pool.Returned);

        stream.Dispose();

        Assert.False(stream.CanRead);
        Assert.False(stream.CanSeek);
        Assert.False(stream.CanWrite);
        Assert.Equal(2, pool.Returned.Count);
        Assert.Same(pool.Rented[0], pool.Returned[0]);
        Assert.Same(pool.Rented[1], pool.Returned[1]);
        Assert.Throws<ObjectDisposedException>(stream.Flush);
        Assert.Throws<ObjectDisposedException>(() => _ = stream.Length);

        stream.Dispose();

        Assert.Equal(2, pool.Returned.Count);
    }

    private sealed class TrackingArrayPool : ArrayPool<byte>
    {
        public List<byte[]> Rented { get; } = [];

        public List<byte[]> Returned { get; } = [];

        public override byte[] Rent(int minimumLength)
        {
            var buffer = new byte[minimumLength];
            this.Rented.Add(buffer);
            return buffer;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            if (clearArray)
            {
                Array.Clear(array);
            }

            this.Returned.Add(array);
        }
    }
}

#endif
