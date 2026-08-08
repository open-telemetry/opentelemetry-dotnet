// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public class SerializationBufferTests
{
    [Fact]
    public void ReturnUsesSerializedLengthForNextRental()
    {
        const int InitialSize = 1024;
        const int SerializedLength = 2048;

        var serializationBuffer = new SerializationBuffer(InitialSize);
        var oversizedBuffer = ProtobufSerializer.RentBuffer(8 * 1024 * 1024);
        serializationBuffer.Return(oversizedBuffer, SerializedLength);

        var nextBuffer = serializationBuffer.Rent();

        Assert.Equal(SerializedLength, nextBuffer.Length);

        serializationBuffer.Return(nextBuffer, 0);
        serializationBuffer.Release();
    }

    [Fact]
    public void DiscardResetsNextRentalToInitialSize()
    {
        const int InitialSize = 1024;

        var serializationBuffer = new SerializationBuffer(InitialSize);
        var oversizedBuffer = ProtobufSerializer.RentBuffer(8 * 1024 * 1024);
        serializationBuffer.Discard(oversizedBuffer);

        var nextBuffer = serializationBuffer.Rent();

        Assert.Equal(InitialSize, nextBuffer.Length);

        serializationBuffer.Return(nextBuffer, 0);
        serializationBuffer.Release();
    }

    [Fact]
    public void ReturnScrubsSerializedPayloadAndLeavesRemainderUntouched()
    {
        const int SerializedLength = 128;

        var serializationBuffer = new SerializationBuffer(1024);
        var buffer = ProtobufSerializer.RentBuffer(4096);
        buffer.AsSpan().Fill(0xAB);

        serializationBuffer.Return(buffer, SerializedLength);

        // The serialized payload must never be handed to another consumer of the pool.
        Assert.All(
            buffer.AsSpan(0, SerializedLength).ToArray(),
            static value => Assert.Equal(0, value));

        // The bytes past the payload were never written, so clearing them would be wasted work.
        Assert.Equal(0xAB, buffer[SerializedLength]);
        Assert.Equal(0xAB, buffer[buffer.Length - 1]);

        serializationBuffer.Release();
    }

    [Fact]
    public void DiscardScrubsWholeBuffer()
    {
        var serializationBuffer = new SerializationBuffer(1024);
        var buffer = ProtobufSerializer.RentBuffer(4096);
        buffer.AsSpan().Fill(0xAB);

        // How far the writer got is unknown after a failure, so everything is scrubbed.
        serializationBuffer.Discard(buffer);

        Assert.All(buffer, static value => Assert.Equal(0, value));

        serializationBuffer.Release();
    }

#if NETFRAMEWORK
    [Fact]
    public void ReturnRetainsWellUtilizedOversizedBuffer()
    {
        const int BufferSize = 2 * 1024 * 1024;
        const int SerializedLength = 3 * 1024 * 1024 / 2;

        var serializationBuffer = new SerializationBuffer(1024);
        var oversizedBuffer = ProtobufSerializer.RentBuffer(BufferSize);
        serializationBuffer.Return(oversizedBuffer, SerializedLength);

        var nextBuffer = serializationBuffer.Rent();

        Assert.Same(oversizedBuffer, nextBuffer);

        serializationBuffer.Return(nextBuffer, 0);
        serializationBuffer.Release();
    }

    [Fact]
    public void ReturnScrubsPayloadCarriedOverByRetainedBuffer()
    {
        const int BufferSize = 2 * 1024 * 1024;
        const int LargeSerializedLength = 3 * 1024 * 1024 / 2;
        const int SmallSerializedLength = 128;

        var serializationBuffer = new SerializationBuffer(1024);
        var oversizedBuffer = ProtobufSerializer.RentBuffer(BufferSize);
        oversizedBuffer.AsSpan(0, LargeSerializedLength).Fill(0xAB);

        // A large export retains the buffer, leaving its payload in place.
        serializationBuffer.Return(oversizedBuffer, LargeSerializedLength);
        Assert.Same(oversizedBuffer, serializationBuffer.Rent());

        // A much smaller export then hands the same buffer back to the pool. The
        // earlier, larger payload must be scrubbed too, not just the new one.
        serializationBuffer.Return(oversizedBuffer, SmallSerializedLength);

        Assert.All(
            oversizedBuffer.AsSpan(0, LargeSerializedLength).ToArray(),
            static value => Assert.Equal(0, value));

        serializationBuffer.Release();
    }

    [Fact]
    public void ReleaseScrubsRetainedPayload()
    {
        const int BufferSize = 2 * 1024 * 1024;
        const int SerializedLength = 3 * 1024 * 1024 / 2;

        var serializationBuffer = new SerializationBuffer(1024);
        var oversizedBuffer = ProtobufSerializer.RentBuffer(BufferSize);
        oversizedBuffer.AsSpan(0, SerializedLength).Fill(0xAB);

        serializationBuffer.Return(oversizedBuffer, SerializedLength);
        serializationBuffer.Release();

        Assert.All(
            oversizedBuffer.AsSpan(0, SerializedLength).ToArray(),
            static value => Assert.Equal(0, value));
    }
#endif
}
