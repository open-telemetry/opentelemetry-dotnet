// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public class WritingPrimitivesTests
{
    [Fact]
    public void GetTagValue_ReturnsCorrectValue()
    {
        Assert.Equal(8u, WritingPrimitives.GetTagValue(1, WireType.VARINT));
        Assert.Equal(17u, WritingPrimitives.GetTagValue(2, WireType.I64));
        Assert.Equal(26u, WritingPrimitives.GetTagValue(3, WireType.LEN));
    }

    [Fact]
    public void WriteTag_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteTag(ref buffer, 0, 1, WireType.VARINT);
        Assert.Equal(1, position);
        Assert.Equal(8, buffer[0]);
    }

    [Fact]
    public void WriteLength_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteLength(ref buffer, 0, 300);
        Assert.Equal(2, position);
        Assert.Equal(0xAC, buffer[0]);
        Assert.Equal(0x02, buffer[1]);
    }

    [Fact]
    public void WriteBoolWithTag_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteBoolWithTag(ref buffer, 0, 1, true);
        Assert.Equal(2, position);
        Assert.Equal(8, buffer[0]);
        Assert.Equal(1, buffer[1]);
    }

    [Fact]
    public void WriteFixed32WithTag_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteFixed32WithTag(ref buffer, 0, 1, 0x12345678);
        Assert.Equal(5, position);
        Assert.Equal(13, buffer[0]);
        Assert.Equal(0x78, buffer[1]);
        Assert.Equal(0x56, buffer[2]);
        Assert.Equal(0x34, buffer[3]);
        Assert.Equal(0x12, buffer[4]);
    }

    [Fact]
    public void WriteFixed64WithTag_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteFixed64WithTag(ref buffer, 0, 1, 0x123456789ABCDEF0);
        Assert.Equal(9, position);
        Assert.Equal(9, buffer[0]); // Tag
        Assert.Equal(0xF0, buffer[1]);
        Assert.Equal(0xDE, buffer[2]);
        Assert.Equal(0xBC, buffer[3]);
        Assert.Equal(0x9A, buffer[4]);
        Assert.Equal(0x78, buffer[5]);
        Assert.Equal(0x56, buffer[6]);
        Assert.Equal(0x34, buffer[7]);
        Assert.Equal(0x12, buffer[8]);
    }

    [Fact]
    public void WriteStringWithTag_WritesCorrectly()
    {
        byte[] buffer = new byte[20];
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, "Hello");
        Assert.Equal(7, position);
        Assert.Equal(10, buffer[0]);
        Assert.Equal(5, buffer[1]);
        Assert.Equal((byte)'H', buffer[2]);
        Assert.Equal((byte)'e', buffer[3]);
        Assert.Equal((byte)'l', buffer[4]);
        Assert.Equal((byte)'l', buffer[5]);
        Assert.Equal((byte)'o', buffer[6]);
    }

    [Fact]
    public void EnsureBufferCapacity_IncreasesBufferSize()
    {
        byte[] buffer = new byte[10];
        buffer = WritingPrimitives.EnsureBufferCapacity(ref buffer, 20);
        Assert.Equal(20, buffer.Length);
    }

    [Theory]
    [InlineData(300, new byte[] { 0xAC, 0x82, 0x80, 0x00 })] // Normal case with 300
    [InlineData(127, new byte[] { 0xFF, 0x80, 0x80, 0x00 })] // Boundary case: max 1-byte value
    [InlineData(128, new byte[] { 0x80, 0x81, 0x80, 0x00 })] // Boundary case: min 2-byte value
    [InlineData(16383, new byte[] { 0xFF, 0xFF, 0x80, 0x00 })] // Max 2-byte value
    [InlineData(16384, new byte[] { 0x80, 0x80, 0x81, 0x00 })] // Min 3-byte value
    [InlineData(2097151, new byte[] { 0xFF, 0xFF, 0xFF, 0x00 })] // Max 3-byte value
    [InlineData(2097152, new byte[] { 0x80, 0x80, 0x80, 0x01 })] // Min 4-byte value
    [InlineData(268435455, new byte[] { 0xFF, 0xFF, 0xFF, 0x7F })] // Max 4-byte value
    public void WriteReservedLength_WritesCorrectly(int length, byte[] expectedBytes)
    {
        byte[] buffer = new byte[10];
        WritingPrimitives.WriteReservedLength(ref buffer, 0, length);

        for (int i = 0; i < expectedBytes.Length; i++)
        {
            Assert.Equal(expectedBytes[i], buffer[i]);
        }
    }

    [Fact]
    public void WriteTagAndLengthPrefix_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteTagAndLengthPrefix(ref buffer, 0, 300, 1, WireType.LEN);
        Assert.Equal(3, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(0xAC, buffer[1]); // Length (300 in varint encoding)
        Assert.Equal(0x02, buffer[2]);
    }

    [Fact]
    public void WriteEnumWithTag_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteEnumWithTag(ref buffer, 0, 1, 5);
        Assert.Equal(2, position);
        Assert.Equal(8, buffer[0]); // Tag
        Assert.Equal(5, buffer[1]); // Enum value
    }

    [Fact]
    public void WriteVarInt64_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteVarInt64(ref buffer, 0, 300);
        Assert.Equal(2, position);
        Assert.Equal(0xAC, buffer[0]);
        Assert.Equal(0x02, buffer[1]);
    }

    [Fact]
    public void WriteInt64WithTag_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteInt64WithTag(ref buffer, 0, 1, 300);
        Assert.Equal(3, position);
        Assert.Equal(8, buffer[0]); // Tag
        Assert.Equal(0xAC, buffer[1]);
        Assert.Equal(0x02, buffer[2]);
    }

    [Fact]
    public void WriteDoubleWithTag_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteDoubleWithTag(ref buffer, 0, 1, 123.456);
        Assert.Equal(9, position);
        Assert.Equal(9, buffer[0]); // Tag

        // The next 8 bytes represent 123.456 in IEEE 754 double-precision format
        Assert.Equal(0x77, buffer[1]);
        Assert.Equal(0xBE, buffer[2]);
        Assert.Equal(0x9F, buffer[3]);
        Assert.Equal(0x1A, buffer[4]);
        Assert.Equal(0x2F, buffer[5]);
        Assert.Equal(0xDD, buffer[6]);
        Assert.Equal(0x5E, buffer[7]);
        Assert.Equal(0x40, buffer[8]);
    }

    [Fact]
    public void WriteVarInt32_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteVarInt32(ref buffer, 0, 300);
        Assert.Equal(2, position);
        Assert.Equal(0xAC, buffer[0]);
        Assert.Equal(0x02, buffer[1]);
    }

    [Fact]
    public void WriteSingleByte_WritesCorrectly()
    {
        byte[] buffer = new byte[1];
        int position = WritingPrimitives.WriteSingleByte(ref buffer, 0, 0xAA);
        Assert.Equal(1, position);
        Assert.Equal(0xAA, buffer[0]);
    }

    [Fact]
    public void IncreaseBufferSize_DoublesBufferSize()
    {
        byte[] buffer = new byte[10];
        bool result = WritingPrimitives.IncreaseBufferSize(ref buffer);
        Assert.True(result);
        Assert.Equal(20, buffer.Length);
    }

    [Fact]
    public void IncreaseBufferSize_Should_Fail_If_Buffer_Too_Large()
    {
        byte[] buffer = new byte[100 * 1024 * 1024]; // 100 MB buffer
        bool success = WritingPrimitives.IncreaseBufferSize(ref buffer);

        Assert.False(success); // Should fail to grow buffer beyond 100MB
    }

    [Fact]
    public void WriteVarInt32_MaxValue_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteVarInt32(ref buffer, 0, uint.MaxValue);
        Assert.Equal(5, position);
        Assert.Equal(0xFF, buffer[0]);
        Assert.Equal(0xFF, buffer[1]);
        Assert.Equal(0xFF, buffer[2]);
        Assert.Equal(0xFF, buffer[3]);
        Assert.Equal(0x0F, buffer[4]);
    }

    [Fact]
    public void WriteVarInt64_MaxValue_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteVarInt64(ref buffer, 0, ulong.MaxValue);
        Assert.Equal(10, position);
        for (int i = 0; i < 9; i++)
        {
            Assert.Equal(0xFF, buffer[i]);
        }

        Assert.Equal(0x01, buffer[9]);
    }

    [Fact]
    public void WriteStringWithTag_EmptyString_WritesCorrectly()
    {
        byte[] buffer = new byte[10];
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, string.Empty);
        Assert.Equal(2, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(0, buffer[1]); // Length
    }

    [Fact]
    public void WriteStringWithTag_ASCIIString_WritesCorrectly()
    {
        byte[] buffer = new byte[20];
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, "Hello");
        Assert.Equal(7, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(5, buffer[1]); // Length

        byte[] expectedContent = Encoding.ASCII.GetBytes("Hello");
        byte[] actualContent = new byte[5];
        Array.Copy(buffer, 2, actualContent, 0, 5);
        Assert.True(expectedContent.SequenceEqual(actualContent));
    }

    [Fact]
    public void WriteStringWithTag_UnicodeString_WritesCorrectly()
    {
        byte[] buffer = new byte[20];
        string unicodeString = "こんにちは"; // "Hello" in Japanese
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, unicodeString);
        Assert.Equal(17, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(15, buffer[1]); // Length (3 bytes per character in UTF-8)

        byte[] expectedContent = Encoding.UTF8.GetBytes(unicodeString);
        byte[] actualContent = new byte[15];
        Array.Copy(buffer, 2, actualContent, 0, 15);
        Assert.True(expectedContent.SequenceEqual(actualContent));
    }

    [Fact]
    public void WriteStringWithTag_LongString_WritesCorrectly()
    {
        string longString = new string('a', 1000);
        byte[] buffer = new byte[1100];
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, longString);
        Assert.Equal(1003, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(0xE8, buffer[1]); // Length (1000 in varint encoding)
        Assert.Equal(0x07, buffer[2]);

        byte[] expectedContent = Encoding.UTF8.GetBytes(longString);
        byte[] actualContent = new byte[1000];
        Array.Copy(buffer, 3, actualContent, 0, 1000);
        Assert.True(expectedContent.SequenceEqual(actualContent));
    }

    [Fact]
    public void WriteStringWithTag_MixedEncodingString_WritesCorrectly()
    {
        byte[] buffer = new byte[30];
        string mixedString = "Hello世界"; // "Hello World" with "World" in Chinese
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, mixedString);
        Assert.Equal(13, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(11, buffer[1]); // Length (5 for "Hello" + 6 for "世界" in UTF-8)

        byte[] expectedContent = Encoding.UTF8.GetBytes(mixedString);
        byte[] actualContent = new byte[11];
        Array.Copy(buffer, 2, actualContent, 0, 11);
        Assert.True(expectedContent.SequenceEqual(actualContent));
    }

    [Fact]
    public void WriteStringWithTag_StringWithSpecialCharacters_WritesCorrectly()
    {
        byte[] buffer = new byte[30];
        string specialString = "Hello\n\t\"World\"";
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, specialString);
        Assert.Equal(16, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(14, buffer[1]); // Length

        byte[] expectedContent = Encoding.UTF8.GetBytes(specialString);
        byte[] actualContent = new byte[14];
        Array.Copy(buffer, 2, actualContent, 0, 14);
        Assert.True(expectedContent.SequenceEqual(actualContent));
    }

    [Fact]
    public void WriteStringWithTag_VeryLongString_ExpandsBuffer()
    {
        string veryLongString = new string('a', 1_000_000);
        byte[] buffer = new byte[100]; // Start with a small buffer
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, veryLongString);
        Assert.Equal(1_000_004, position);
        Assert.True(buffer.Length >= 1_000_004);
        Assert.Equal(10, buffer[0]); // Tag

        // Check the length (1,000,000 in varint encoding)
        Assert.Equal(0xC0, buffer[1]);
        Assert.Equal(0x84, buffer[2]);
        Assert.Equal(0x3D, buffer[3]);

        // Check the content
        for (int i = 4; i < position; i++)
        {
            Assert.Equal((byte)'a', buffer[i]);
        }
    }

    [Fact]
    public void WriteStringWithTag_StringWithNullCharacters_WritesCorrectly()
    {
        byte[] buffer = new byte[20];
        string stringWithNull = "Hello\0World";
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, stringWithNull);
        Assert.Equal(13, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(11, buffer[1]); // Length

        byte[] expectedContent = Encoding.UTF8.GetBytes(stringWithNull);
        byte[] actualContent = new byte[11];
        Array.Copy(buffer, 2, actualContent, 0, 11);
        Assert.True(expectedContent.SequenceEqual(actualContent));
    }

    [Fact]
    public void WriteStringWithTag_StringExceedingBufferCapacity_ExpandsBuffer()
    {
        // Create a buffer that's not large enough for the entire string
        string str = "HelloWorld";
        byte[] buffer = new byte[7]; // Not enough space for the entire string
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, str);
        Assert.Equal(12, position);
        Assert.True(buffer.Length >= 12);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(10, buffer[1]); // Length

        byte[] expectedContent = Encoding.UTF8.GetBytes(str);
        byte[] actualContent = new byte[10];
        Array.Copy(buffer, 2, actualContent, 0, 10);
        Assert.True(expectedContent.SequenceEqual(actualContent));
    }

    [Fact]
    public void WriteStringWithTag_SurrogatePairs_WritesCorrectly()
    {
        byte[] buffer = new byte[20];
        string surrogatePairString = "📚"; // Books emoji
        int position = WritingPrimitives.WriteStringWithTag(ref buffer, 0, 1, surrogatePairString);
        Assert.Equal(6, position);
        Assert.Equal(10, buffer[0]); // Tag
        Assert.Equal(4, buffer[1]); // Length (4 bytes for the surrogate pair in UTF-8)

        byte[] expectedContent = Encoding.UTF8.GetBytes(surrogatePairString);
        byte[] actualContent = new byte[4];
        Array.Copy(buffer, 2, actualContent, 0, 4);
        Assert.True(expectedContent.SequenceEqual(actualContent));
    }
}
