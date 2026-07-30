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
#endif
}
