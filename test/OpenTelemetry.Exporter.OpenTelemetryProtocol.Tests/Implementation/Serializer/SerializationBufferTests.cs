// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public class SerializationBufferTests
{
    [Fact]
    public void ReturnUsesSerializedLengthForNextRental()
    {
        const int InitialSize = 1024;
        const int SerializedLength = 2048;

        var pool = new TrackingArrayPool();
        var serializationBuffer = new SerializationBuffer(InitialSize, pool);
        var oversizedBuffer = pool.Rent(8 * 1024 * 1024);

        serializationBuffer.Return(oversizedBuffer, SerializedLength);

        var nextBuffer = serializationBuffer.Rent();

        Assert.Equal(SerializedLength, nextBuffer.Length);
    }

    [Fact]
    public void DiscardResetsNextRentalToInitialSize()
    {
        const int InitialSize = 1024;

        var pool = new TrackingArrayPool();
        var serializationBuffer = new SerializationBuffer(InitialSize, pool);
        var oversizedBuffer = pool.Rent(8 * 1024 * 1024);

        serializationBuffer.Discard(oversizedBuffer);

        var nextBuffer = serializationBuffer.Rent();

        Assert.Equal(InitialSize, nextBuffer.Length);
    }

    [Fact]
    public void ReturnScrubsSerializedPayloadAndLeavesRemainderUntouched()
    {
        const int SerializedLength = 128;

        var pool = new TrackingArrayPool();
        var serializationBuffer = new SerializationBuffer(1024, pool);
        var buffer = pool.Rent(4096);
        buffer.AsSpan().Fill(0xAB);

        serializationBuffer.Return(buffer, SerializedLength);

        // The buffer belongs to the pool once it has been handed back, so it is
        // inspected as the pool saw it rather than through the rental.
        var returned = Assert.Single(pool.Returned);

        // The serialized payload must never be handed to another consumer of the pool.
        Assert.All(
            returned.AsSpan(0, SerializedLength).ToArray(),
            static value => Assert.Equal(0, value));

        // The bytes past the payload were never written, so clearing them would be wasted work.
        Assert.Equal(0xAB, returned[SerializedLength]);
        Assert.Equal(0xAB, returned[returned.Length - 1]);
    }

    [Fact]
    public void DiscardScrubsWholeBuffer()
    {
        var pool = new TrackingArrayPool();
        var serializationBuffer = new SerializationBuffer(1024, pool);
        var buffer = pool.Rent(4096);
        buffer.AsSpan().Fill(0xAB);

        // How far the writer got is unknown after a failure, so everything is scrubbed.
        serializationBuffer.Discard(buffer);

        var returned = Assert.Single(pool.Returned);

        Assert.All(returned, static value => Assert.Equal(0, value));
    }

#if NETFRAMEWORK
    [Fact]
    public void ReturnRetainsWellUtilizedOversizedBuffer()
    {
        const int BufferSize = 2 * 1024 * 1024;
        const int SerializedLength = 3 * 1024 * 1024 / 2;

        var pool = new TrackingArrayPool();
        var serializationBuffer = new SerializationBuffer(1024, pool);
        var oversizedBuffer = pool.Rent(BufferSize);

        serializationBuffer.Return(oversizedBuffer, SerializedLength);

        // The buffer is retained rather than handed back to a pool that may not reuse it.
        Assert.Empty(pool.Returned);

        var nextBuffer = serializationBuffer.Rent();

        Assert.Same(oversizedBuffer, nextBuffer);
    }

    [Fact]
    public void ReturnScrubsPayloadCarriedOverByRetainedBuffer()
    {
        const int BufferSize = 2 * 1024 * 1024;
        const int LargeSerializedLength = 3 * 1024 * 1024 / 2;
        const int SmallSerializedLength = 128;

        var pool = new TrackingArrayPool();
        var serializationBuffer = new SerializationBuffer(1024, pool);
        var oversizedBuffer = pool.Rent(BufferSize);
        oversizedBuffer.AsSpan(0, LargeSerializedLength).Fill(0xAB);

        // A large export retains the buffer, leaving its payload in place.
        serializationBuffer.Return(oversizedBuffer, LargeSerializedLength);
        Assert.Same(oversizedBuffer, serializationBuffer.Rent());

        // A much smaller export then hands the same buffer back to the pool. The
        // earlier, larger payload must be scrubbed too, not just the new one.
        serializationBuffer.Return(oversizedBuffer, SmallSerializedLength);

        var returned = Assert.Single(pool.Returned);

        Assert.All(
            returned.AsSpan(0, LargeSerializedLength).ToArray(),
            static value => Assert.Equal(0, value));
    }

    [Fact]
    public void ReleaseScrubsRetainedPayload()
    {
        const int BufferSize = 2 * 1024 * 1024;
        const int SerializedLength = 3 * 1024 * 1024 / 2;

        var pool = new TrackingArrayPool();
        var serializationBuffer = new SerializationBuffer(1024, pool);
        var oversizedBuffer = pool.Rent(BufferSize);
        oversizedBuffer.AsSpan(0, SerializedLength).Fill(0xAB);

        serializationBuffer.Return(oversizedBuffer, SerializedLength);
        serializationBuffer.Release();

        var returned = Assert.Single(pool.Returned);

        Assert.All(
            returned.AsSpan(0, SerializedLength).ToArray(),
            static value => Assert.Equal(0, value));
    }
#endif

    private sealed class TrackingArrayPool : ArrayPool<byte>
    {
        public List<byte[]> Returned { get; } = [];

        public override byte[] Rent(int minimumLength) => new byte[minimumLength];

        public override void Return(byte[] array, bool clearArray = false)
        {
            if (clearArray)
            {
                array.AsSpan().Clear();
            }

            this.Returned.Add(array);
        }
    }
}
