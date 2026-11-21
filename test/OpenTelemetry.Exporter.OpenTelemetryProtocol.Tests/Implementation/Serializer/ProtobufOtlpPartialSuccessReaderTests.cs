// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public class ProtobufOtlpPartialSuccessReaderTests
{
    public static TheoryData<byte[]> MalformedResponses() => new()
    {
        new byte[] { 0x0A }, // tag without length
        new byte[] { 0x0A, 0x05, 0x08 }, // length beyond the end
        new byte[] { 0x0A, 0x02, 0x08, 0x80 }, // varint cut short
        new byte[] { 0x0A, 0x0B, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF }, // varint over 10 bytes
        new byte[] { 0x0A, 0x0B, 0x08, 0x81, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02 }, // ten-byte varint overflowing 64 bits
        new byte[] { 0x0A, 0x0B, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, // ten-byte varint overflowing 64 bits
        new byte[] { 0x8A, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02, 0x00 }, // overflowing tag
        new byte[] { 0x0A, 0x82, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02, 0x08, 0x01 }, // overflowing length
        new byte[] { 0x0A, 0x02, 0x12, 0x05 }, // string beyond the end of the partial success
        new byte[] { 0x0B, 0x0C }, // group start
        new byte[] { 0x0F }, // invalid wire type
        new byte[] { 0x00 }, // field number zero
        new byte[] { 0x19, 0x01, 0x02 }, // fixed64 cut short
        new byte[] { 0x1D, 0x01 }, // fixed32 cut short
        new byte[] { 0x0A, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, // length larger than int.MaxValue
        "not a protobuf message"u8.ToArray(),
    };

    [Fact]
    public void TryRead_TenByteVarInt_ReadsHighestBit()
    {
        // rejected = 0x8000000000000001 (as int64, long.MinValue + 1) encoded in the maximum ten bytes.
        byte[] response = [0x0A, 0x0B, 0x08, 0x81, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01];

        Assert.True(ProtobufOtlpPartialSuccessReader.TryRead(response, 0, response.Length, out var rejected, out _));
        Assert.Equal(long.MinValue + 1, rejected);
    }

    [Fact]
    public void TryRead_EmptyResponse_IsPlainSuccess()
    {
        Assert.True(ProtobufOtlpPartialSuccessReader.TryRead([], 0, 0, out var rejected, out var message));
        Assert.Equal(0, rejected);
        Assert.Null(message);
    }

    [Fact]
    public void TryRead_EmptyPartialSuccess_IsPlainSuccess()
    {
        var response = PartialSuccessResponse.Create(rejected: null, errorMessage: null);

        Assert.True(ProtobufOtlpPartialSuccessReader.TryRead(response, 0, response.Length, out var rejected, out var message));
        Assert.Equal(0, rejected);
        Assert.Null(message);
    }

    [Theory]
    [InlineData(1L, "rejected")]
    [InlineData(42L, null)]
    [InlineData(0L, "warning")]
    [InlineData(long.MaxValue, "max")]
    [InlineData(300L, "\u00e9t\u00e9")]
    public void TryRead_PartialSuccess_ReadsFields(long expectedRejected, string? expectedMessage)
    {
        var response = PartialSuccessResponse.Create(expectedRejected, expectedMessage);

        Assert.True(ProtobufOtlpPartialSuccessReader.TryRead(response, 0, response.Length, out var rejected, out var message));
        Assert.Equal(expectedRejected, rejected);
        Assert.Equal(expectedMessage, message);
    }

    [Fact]
    public void TryRead_RespectsOffsetAndCount()
    {
        var response = PartialSuccessResponse.Create(7, "msg");
        var buffer = Enumerable.Repeat((byte)0xFF, response.Length + 6).ToArray();
        Array.Copy(response, 0, buffer, 3, response.Length);

        Assert.True(ProtobufOtlpPartialSuccessReader.TryRead(buffer, 3, response.Length, out var rejected, out var message));
        Assert.Equal(7, rejected);
        Assert.Equal("msg", message);
    }

    [Fact]
    public void TryRead_SkipsUnknownFields()
    {
        byte[] inner =
        [
            .. new byte[] { 0x18, 0x96, 0x01 }, // field 3, varint 150
            .. new byte[] { 0x21, 1, 2, 3, 4, 5, 6, 7, 8 }, // field 4, fixed64
            .. new byte[] { 0x2D, 1, 2, 3, 4 }, // field 5, fixed32
            .. new byte[] { 0x32, 0x02, 0xAA, 0xBB }, // field 6, length-delimited
            .. new byte[] { 0x08, 0x05 }, // rejected = 5
            .. new byte[] { 0x10, 0x01 }, // field 2 with the wrong wire type
        ];

        byte[] response =
        [
            .. new byte[] { 0x12, 0x01, 0x00 }, // unknown field 2 of the response
            0x0A,
            (byte)inner.Length,
            .. inner,
        ];

        Assert.True(ProtobufOtlpPartialSuccessReader.TryRead(response, 0, response.Length, out var rejected, out var message));
        Assert.Equal(5, rejected);
        Assert.Null(message);
    }

    [Fact]
    public void TryRead_RepeatedPartialSuccess_IsMerged()
    {
        byte[] response =
        [
            .. PartialSuccessResponse.Create(1, "first"),
            .. PartialSuccessResponse.Create(2, null),
        ];

        Assert.True(ProtobufOtlpPartialSuccessReader.TryRead(response, 0, response.Length, out var rejected, out var message));
        Assert.Equal(2, rejected);
        Assert.Equal("first", message);
    }

    [Theory]
    [MemberData(nameof(MalformedResponses))]
    public void TryRead_MalformedResponse_ReturnsFalse(byte[] response)
    {
        var exception = Record.Exception(() =>
            Assert.False(ProtobufOtlpPartialSuccessReader.TryRead(response, 0, response.Length, out _, out _)));

        Assert.Null(exception);
    }

    [Fact]
    public void TryRead_TruncatedResponse_ReturnsFalse()
    {
        var response = PartialSuccessResponse.Create(12345, "a message that is cut short");

        for (var length = 1; length < response.Length; length++)
        {
            Assert.False(ProtobufOtlpPartialSuccessReader.TryRead(response, 0, length, out _, out _));
        }
    }

    [Fact]
    public void TryRead_InvalidRange_ReturnsFalse()
    {
        var response = PartialSuccessResponse.Create(1, "x");

        Assert.False(ProtobufOtlpPartialSuccessReader.TryRead(response, 1, response.Length, out _, out _));
        Assert.False(ProtobufOtlpPartialSuccessReader.TryRead(response, -1, 1, out _, out _));
        Assert.False(ProtobufOtlpPartialSuccessReader.TryRead(response, 0, -1, out _, out _));
    }
}
