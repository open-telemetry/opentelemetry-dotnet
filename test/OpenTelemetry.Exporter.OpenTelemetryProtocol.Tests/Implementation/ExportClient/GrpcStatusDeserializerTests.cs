// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using Type = System.Type;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.ExportClient;

public class GrpcStatusDeserializerTests
{
    [Fact]
    public void DeserializeStatus_ValidBase64Input_ReturnsExpectedStatus()
    {
        var status = new Google.Rpc.Status
        {
            Code = 5,
            Message = "Test error",
            Details =
            {
                Any.Pack(new StringValue { Value = "Example detail" }),
            },
        };

        // Serialize the Status message and encode to base64
        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Use the GrpcStatusDeserializer to deserialize from the base64 input
        var deserializedStatus = GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin);

        // Assertions to validate the deserialized Status object
        Assert.NotNull(deserializedStatus);
        Assert.Equal(status.Code, deserializedStatus.Value.Code);
        Assert.Equal(status.Message, deserializedStatus.Value.Message);
        var singleDetail = Assert.Single(deserializedStatus.Value.Details);
        Assert.Equal("type.googleapis.com/google.protobuf.StringValue", singleDetail.TypeUrl);
        var stringValue = StringValue.Parser.ParseFrom(singleDetail.Value);
        Assert.Equal("Example detail", stringValue.Value);
    }

    [Fact]
    public void DeserializeStatus_WithRetryInfo_ReturnsExpectedStatus()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = 4,
            Message = "Retry later",
            Details =
                {
                    Any.Pack(new Google.Rpc.RetryInfo
                    {
                        RetryDelay = new Duration { Seconds = 5 },
                    }),
                },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var retryInfo = GrpcStatusDeserializer.ExtractRetryInfo(grpcStatusDetailsBin);

        // Assert
        Assert.NotNull(retryInfo);
        Assert.Equal(5, retryInfo.Value.RetryDelay?.Seconds);
    }

    [Fact]
    public void DeserializeStatus_EmptyStatus_ReturnsEmptyStatus()
    {
        // Arrange
        var status = new Google.Rpc.Status();
        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var deserializedStatus = GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin);

        // Assert
        Assert.Null(deserializedStatus);
    }

    [Fact]
    public void DeserializeStatus_MultipleDetails_ReturnsAllDetails()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = 7,
            Message = "Multiple details",
            Details =
                {
                    Any.Pack(new StringValue { Value = "First detail" }),
                    Any.Pack(new Google.Rpc.RetryInfo
                    {
                        RetryDelay = new Duration { Seconds = 10 },
                    }),
                },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var deserializedStatus = GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin);
        var retryInfo = GrpcStatusDeserializer.ExtractRetryInfo(grpcStatusDetailsBin);

        // Assert
        Assert.NotNull(deserializedStatus);
        Assert.Equal(status.Code, deserializedStatus.Value.Code);
        Assert.Equal(status.Message, deserializedStatus.Value.Message);
        Assert.Equal(2, deserializedStatus.Value.Details.Count);

        // Verify first detail (StringValue)
        Assert.Equal("type.googleapis.com/google.protobuf.StringValue", deserializedStatus.Value.Details[0].TypeUrl);
        var stringValue = StringValue.Parser.ParseFrom(deserializedStatus.Value.Details[0].Value);
        Assert.Equal("First detail", stringValue.Value);

        // Verify second detail (RetryInfo)
        Assert.Equal("type.googleapis.com/google.rpc.RetryInfo", deserializedStatus.Value.Details[1].TypeUrl);
        Assert.NotNull(retryInfo);
        Assert.Equal(10, retryInfo.Value.RetryDelay?.Seconds);
    }

    [Fact]
    public void DeserializeStatus_ComplexRetryInfo_ReturnsExpectedValues()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = 4,
            Message = "Complex retry scenario",
            Details =
                {
                    Any.Pack(new Google.Rpc.RetryInfo
                    {
                        RetryDelay = new Duration
                        {
                            Seconds = 5,
                            Nanos = 500000000, // 0.5 seconds
                        },
                    }),
                },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var retryInfo = GrpcStatusDeserializer.ExtractRetryInfo(grpcStatusDetailsBin);

        // Assert
        Assert.NotNull(retryInfo);
        Assert.Equal(5, retryInfo.Value.RetryDelay?.Seconds);
        Assert.Equal(500000000, retryInfo.Value.RetryDelay?.Nanos);
    }

    [Fact]
    public void ExtractRetryInfo_WithNoRetryInfoTypeUrl_ReturnsNull()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = 3,
            Message = "No retry info",
            Details = { Any.Pack(new Google.Rpc.Status { Code = 5 }) }, // A different type packed
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var retryInfo = GrpcStatusDeserializer.ExtractRetryInfo(grpcStatusDetailsBin);

        // Assert
        Assert.Null(retryInfo);
    }

    [Fact]
    public void DeserializeStatus_WithBoundaryCode_ReturnsExpectedStatus()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = int.MaxValue,
            Message = "Boundary code test",
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var deserializedStatus = GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin);

        // Assert
        Assert.NotNull(deserializedStatus);
        Assert.Equal(int.MaxValue, deserializedStatus.Value.Code);
        Assert.Equal("Boundary code test", deserializedStatus.Value.Message);
    }

    [Fact]
    public void TryGetGrpcRetryDelay_NullOrEmptyInput_ReturnsNull()
    {
        Assert.Null(GrpcStatusDeserializer.TryGetGrpcRetryDelay(null));
        Assert.Null(GrpcStatusDeserializer.TryGetGrpcRetryDelay(string.Empty));
        Assert.Null(GrpcStatusDeserializer.TryGetGrpcRetryDelay(" "));
    }

    [Fact]
    public void TryGetGrpcRetryDelay_InvalidBase64Input_ReturnsNull()
        => Assert.Null(GrpcStatusDeserializer.TryGetGrpcRetryDelay("invalid-base64"));

    [Fact]
    public void TryGetGrpcRetryDelay_NoRetryInfo_ReturnsNull()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = 3,
            Message = "No retry info",
            Details = { Any.Pack(new Google.Rpc.Status { Code = 5 }) }, // Non-RetryInfo type
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var result = GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetGrpcRetryDelay_BoundaryValuesForDuration_ReturnsNull()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = 4,
            Message = "Boundary test",
            Details =
        {
            Any.Pack(new Google.Rpc.RetryInfo
            {
                RetryDelay = new Duration
                {
                    Seconds = long.MaxValue,
                    Nanos = int.MaxValue,
                },
            }),
        },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var result = GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryGetGrpcRetryDelay_MultipleRetryInfos_UsesFirstRetryInfo()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = 4,
            Message = "Multiple RetryInfos",
            Details =
        {
            Any.Pack(new Google.Rpc.RetryInfo
            {
                RetryDelay = new Duration { Seconds = 5 },
            }),
            Any.Pack(new Google.Rpc.RetryInfo
            {
                RetryDelay = new Duration { Seconds = 10 },
            }),
        },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var result = GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromSeconds(5), result);
    }

    [Theory]
    [InlineData(8, 8, true)]
    [InlineData(14, 14, true)]
    [InlineData(0, 8, true)]
    [InlineData(3, 8, false)]
    [InlineData(8, 14, false)]
    [InlineData(-1, 8, false)]
    [InlineData(int.MaxValue, 8, false)]
    public void TryGetGrpcRetryDelay_StatusDetailsCode_MustNotContradictGrpcStatus(int detailsCode, int grpcStatus, bool expectDelay)
    {
        var status = new Google.Rpc.Status
        {
            Code = detailsCode,
            Details =
            {
                Any.Pack(new Google.Rpc.RetryInfo
                {
                    RetryDelay = new Duration { Seconds = 5 },
                }),
            },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        var result = GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin, (StatusCode)grpcStatus);

        Assert.Equal(expectDelay ? TimeSpan.FromSeconds(5) : null, result);
        Assert.Equal(TimeSpan.FromSeconds(5), GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin));
    }

    [Theory]
    [InlineData(new byte[0], 8, true)]
    [InlineData(new byte[] { 0x08, 0x08 }, 8, true)]
    [InlineData(new byte[] { 0x08, 0x03 }, 8, false)]
    [InlineData(new byte[] { 0x08, 0x00 }, 8, false)]
    [InlineData(new byte[] { 0x08, 0x00 }, 14, false)]
    public void TryGetGrpcRetryDelay_StatusDetailsCodePresenceOnTheWire_IsHonored(byte[] statusCodeField, int grpcStatus, bool expectDelay)
    {
        // Google.Rpc.Status does not serialize a code of zero, so the status is encoded by hand to distinguish an explicit zero from an omitted code
        var retryInfo = new Google.Rpc.RetryInfo
        {
            RetryDelay = new Duration { Seconds = 5 },
        };

        var detail = new List<byte>();
        detail.AddRange(EncodeLengthDelimitedField(1, "type.googleapis.com/google.rpc.RetryInfo"u8.ToArray()));
        detail.AddRange(EncodeLengthDelimitedField(2, retryInfo.ToByteArray()));

        var status = new List<byte>([.. statusCodeField]);
        status.AddRange(EncodeLengthDelimitedField(3, [.. detail]));

        var grpcStatusDetailsBin = Convert.ToBase64String([.. status]);

        var result = GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin, (StatusCode)grpcStatus);

        Assert.Equal(expectDelay ? TimeSpan.FromSeconds(5) : null, result);
        Assert.Equal(TimeSpan.FromSeconds(5), GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin));
    }

    [Theory]
    [InlineData(-1, 0)] // -1s
    [InlineData(0, -1_000_000)] // -1ms, which is also Timeout.InfiniteTimeSpan
    [InlineData(-5, -500_000_000)] // -5.5s
    public void TryGetGrpcRetryDelay_NegativeDuration_ClampedToZero(long seconds, int nanos)
    {
        var status = new Google.Rpc.Status
        {
            Code = 8, // ResourceExhausted
            Message = "Negative retry delay",
            Details =
            {
                Any.Pack(new Google.Rpc.RetryInfo
                {
                    RetryDelay = new Duration { Seconds = seconds, Nanos = nanos },
                }),
            },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        var result = GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result);
        Assert.Null(Record.Exception(() => Thread.Sleep(result!.Value)));
    }

    [Fact]
    public void TryGetGrpcRetryDelay_OnlyNanos_ReturnsExpected()
    {
        // Arrange
        var status = new Google.Rpc.Status
        {
            Code = 4,
            Message = "Only nanos",
            Details =
            {
                Any.Pack(new Google.Rpc.RetryInfo
                {
                    RetryDelay = new Duration { Nanos = 500000000 }, // 0.5 seconds
                }),
            },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var result = GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromSeconds(0.5), result);
    }

    [Fact]
    public void DeserializeStatus_TruncatedStream_ThrowsException()
    {
        // Arrange: Create valid Base64 data and truncate it
        var status = new Google.Rpc.Status
        {
            Code = 3,
            Message = "Truncated stream test",
        };

        var fullData = status.ToByteArray();
        var truncatedData = fullData.Take(fullData.Length / 2).ToArray(); // Truncate the data

        var grpcStatusDetailsBin = Convert.ToBase64String(truncatedData);

        // Act & Assert: Attempt to deserialize and expect an EndOfStreamException
        Assert.Throws<EndOfStreamException>(() =>
            GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
    }

    [Fact]
    public void DeserializeStatus_WithLargeLengthDelimitedField_ThrowsException()
    {
        // Arrange
        // This payload encodes a Status.details Any.value field with an extremely large
        // length value (0x7FFFFFF0) but without enough bytes in the payload.
        const string grpcStatusDetailsBin = "GgYS8P///wc=";

        // Act & Assert
        Assert.Throws<EndOfStreamException>(() =>
            GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
    }

    [Theory]
    [InlineData("GgsS////////////AQ==")] // -1
    [InlineData("GgYSgICAgAg=")] // 0x80000000
    public void DeserializeStatus_WithInvalidLengthDelimitedField_ThrowsException(string grpcStatusDetailsBin)
    {
        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() => GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
        Assert.Contains("Invalid length", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(int.MaxValue / 2, typeof(EndOfStreamException))]
    [InlineData(int.MaxValue - 1024, typeof(EndOfStreamException))]
    [InlineData(int.MaxValue - 1, typeof(EndOfStreamException))]
    [InlineData(int.MaxValue, typeof(EndOfStreamException))]
    [InlineData(uint.MaxValue, typeof(InvalidDataException))]
    public void DeserializeStatus_InvalidDetailValueLength_Throws(long value, Type expected)
    {
        var anyValueLength = EncodeVarint(value);
        var statusBytes = new byte[2 + 1 + anyValueLength.Length];

        statusBytes[0] = 0x1A; // field 3 (details), wire type 2
        statusBytes[1] = (byte)(1 + anyValueLength.Length); // embedded Any payload length
        statusBytes[2] = 0x12; // field 2 (value), wire type 2
        anyValueLength.CopyTo(statusBytes, 3);

        var grpcStatusDetailsBin = Convert.ToBase64String(statusBytes);

        Assert.Throws(expected, () => GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
    }

    [Fact]
    public void DeserializeStatus_InvalidEmbeddedMessageLength_Throws()
    {
        var statusBytes = new byte[1 + EncodeVarint(long.MaxValue).Length];

        statusBytes[0] = 0x1A; // field 3 (details), wire type 2
        EncodeVarint(long.MaxValue).CopyTo(statusBytes, 1);

        var grpcStatusDetailsBin = Convert.ToBase64String(statusBytes);

        Assert.Throws<InvalidDataException>(() => GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
    }

    [Theory]
    [InlineData(1L << 32)]
    [InlineData((1L << 32) + 3)]
    [InlineData((1L << 33) + 3)]
    [InlineData(long.MaxValue)]
    public void DeserializeStatus_AnyValueLengthThatDoesNotFitInInt32_IsRejected(long declaredLength)
    {
        // The length of the value is that of the 3 bytes which follow it, plus a multiple of 2^32 which is truncated by a cast to int.
        var any = new List<byte>();
        any.AddRange(EncodeLengthDelimitedField(1, "type.googleapis.com/google.rpc.RetryInfo"u8.ToArray()));
        any.Add(0x12);
        any.AddRange(EncodeVarint(declaredLength));
        any.AddRange([0x0A, 0x01, 0x08]);

        var grpcStatusDetailsBin = Convert.ToBase64String(EncodeLengthDelimitedField(3, [.. any]));

        Assert.ThrowsAny<Exception>(() => GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
        Assert.Null(GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin));
    }

    [Theory]
    [InlineData(1L << 32)]
    [InlineData(1L << 33)]
    [InlineData(1L << 40)]
    public void DeserializeStatus_AnyTypeUrlLengthThatDoesNotFitInInt32_IsRejected(long lengthOverflow)
    {
        var typeUrl = "type.googleapis.com/google.rpc.RetryInfo"u8.ToArray();
        var retryInfo = new Google.Rpc.RetryInfo { RetryDelay = new Duration { Seconds = 7 } }.ToByteArray();

        var any = new List<byte> { 0x0A };
        any.AddRange(EncodeVarint(lengthOverflow + typeUrl.Length));
        any.AddRange(typeUrl);
        any.AddRange(EncodeLengthDelimitedField(2, retryInfo));

        var grpcStatusDetailsBin = Convert.ToBase64String(EncodeLengthDelimitedField(3, [.. any]));

        Assert.ThrowsAny<Exception>(() => GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
        Assert.Null(GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin));
    }

    [Theory]
    [InlineData(new byte[] { 0x0A, 0x02, 0xAA, 0xBB })] // code (1) as length-delimited
    [InlineData(new byte[] { 0x09, 1, 2, 3, 4, 5, 6, 7, 8 })] // code (1) as fixed64
    [InlineData(new byte[] { 0x0D, 1, 2, 3, 4 })] // code (1) as fixed32
    [InlineData(new byte[] { 0x10, 0x05 })] // message (2) as varint
    [InlineData(new byte[] { 0x11, 1, 2, 3, 4, 5, 6, 7, 8 })] // message (2) as fixed64
    [InlineData(new byte[] { 0x18, 0x0A })] // details (3) as varint
    [InlineData(new byte[] { 0x19, 1, 2, 3, 4, 5, 6, 7, 8 })] // details (3) as fixed64
    [InlineData(new byte[] { 0x1D, 1, 2, 3, 4 })] // details (3) as fixed32
    public void DeserializeStatus_KnownFieldWithUnexpectedWireType_IsSkipped(byte[] unexpectedField)
    {
        var status = new Google.Rpc.Status
        {
            Code = 4,
            Message = "Retry later",
            Details =
            {
                Any.Pack(new Google.Rpc.RetryInfo { RetryDelay = new Duration { Seconds = 5 } }),
            },
        };

        var grpcStatusDetailsBin = Convert.ToBase64String([.. unexpectedField, .. status.ToByteArray()]);

        var deserializedStatus = GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin);

        Assert.NotNull(deserializedStatus);
        Assert.Equal(4, deserializedStatus.Value.Code);
        Assert.Equal("Retry later", deserializedStatus.Value.Message);
        Assert.Single(deserializedStatus.Value.Details);
        Assert.Equal(TimeSpan.FromSeconds(5), GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin));
    }

    [Theory]
    [InlineData(new byte[] { 0x0A, 0x01, 0xFF })] // seconds (1) as length-delimited
    [InlineData(new byte[] { 0x11, 1, 2, 3, 4, 5, 6, 7, 8 })] // nanos (2) as fixed64
    public void TryGetGrpcRetryDelay_DurationFieldWithUnexpectedWireType_IsSkipped(byte[] unexpectedField)
    {
        var duration = new List<byte>(unexpectedField);
        duration.AddRange([0x08, 0x05]); // seconds = 5

        var retryInfo = EncodeLengthDelimitedField(1, [.. duration]);

        var any = new List<byte>();
        any.AddRange(EncodeLengthDelimitedField(1, "type.googleapis.com/google.rpc.RetryInfo"u8.ToArray()));
        any.AddRange(EncodeLengthDelimitedField(2, retryInfo));

        var grpcStatusDetailsBin = Convert.ToBase64String(EncodeLengthDelimitedField(3, [.. any]));

        Assert.Equal(TimeSpan.FromSeconds(5), GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin));
    }

    [Theory]
    [InlineData(new byte[] { 0x0A, 0x02, 0x08, 0x05, 0x0A, 0x00 }, 5 * TimeSpan.TicksPerSecond)] // seconds = 5, then an empty duration
    [InlineData(new byte[] { 0x0A, 0x00, 0x0A, 0x02, 0x08, 0x05 }, 5 * TimeSpan.TicksPerSecond)] // an empty duration, then seconds = 5
    [InlineData(new byte[] { 0x0A, 0x02, 0x08, 0x05, 0x0A, 0x02, 0x10, 0x64 }, (5 * TimeSpan.TicksPerSecond) + 1)] // seconds = 5, then nanos = 100
    [InlineData(new byte[] { 0x0A, 0x02, 0x08, 0x05, 0x0A, 0x02, 0x08, 0x07 }, 7 * TimeSpan.TicksPerSecond)] // seconds = 5, then seconds = 7
    public void TryGetGrpcRetryDelay_RepeatedRetryDelayFields_AreMerged(byte[] retryInfo, long expectedTicks)
    {
        // A repeated occurrence of a singular message field is merged into the previous one, as any other protobuf decoder would
        var any = new List<byte>();
        any.AddRange(EncodeLengthDelimitedField(1, "type.googleapis.com/google.rpc.RetryInfo"u8.ToArray()));
        any.AddRange(EncodeLengthDelimitedField(2, [.. retryInfo]));

        var grpcStatusDetailsBin = Convert.ToBase64String(EncodeLengthDelimitedField(3, [.. any]));

        Assert.Equal(TimeSpan.FromTicks(expectedTicks), GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin));

        // Cross-check against the reference protobuf implementation
        var expected = Google.Rpc.RetryInfo.Parser.ParseFrom([.. retryInfo]).RetryDelay;
        Assert.Equal(TimeSpan.FromTicks(expectedTicks), expected.ToTimeSpan());
    }

    [Theory]
    [InlineData("")] // Encoded length is a multiple of 3 bytes, so there is no padding
    [InlineData("a")] // One padding character
    [InlineData("ab")] // Two padding characters
    [InlineData("abc")]
    public void TryGetGrpcRetryDelay_PaddedAndUnpaddedBase64_AreEquivalent(string message)
    {
        var status = new Google.Rpc.Status
        {
            Code = 8,
            Message = message,
            Details =
            {
                Any.Pack(new Google.Rpc.RetryInfo
                {
                    RetryDelay = new Duration { Seconds = 5 },
                }),
            },
        };

        var padded = Convert.ToBase64String(status.ToByteArray());
        var unpadded = padded.TrimEnd('=');

        Assert.Equal(TimeSpan.FromSeconds(5), GrpcStatusDeserializer.TryGetGrpcRetryDelay(padded));
        Assert.Equal(TimeSpan.FromSeconds(5), GrpcStatusDeserializer.TryGetGrpcRetryDelay(unpadded));
        Assert.Equal(GrpcStatusDeserializer.DeserializeStatus(padded)!.Value.Message, GrpcStatusDeserializer.DeserializeStatus(unpadded)!.Value.Message);
    }

    [Theory]
    [InlineData("A")] // A single character cannot be Base64
    [InlineData("AAAAA")]
    [InlineData("AA=")] // Partial padding is malformed
    [InlineData("AA=A")]
    [InlineData("AAAA=")]
    [InlineData("A$AA")]
    public void TryGetGrpcRetryDelay_MalformedBase64_ReturnsNull(string grpcStatusDetailsBin)
    {
        Assert.Null(GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin));
        Assert.ThrowsAny<FormatException>(() => GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
    }

    private static byte[] EncodeLengthDelimitedField(int fieldNumber, byte[] value)
    {
        var encoded = new List<byte>();
        encoded.AddRange(EncodeVarint((fieldNumber << 3) | 2));
        encoded.AddRange(EncodeVarint(value.Length));
        encoded.AddRange(value);
        return [.. encoded];
    }

    private static byte[] EncodeVarint(long value)
    {
        var encoded = new List<byte>();
        var remaining = unchecked((ulong)value);

        do
        {
            var current = (byte)(remaining & 0x7F);
            remaining >>= 7;

            if (remaining != 0)
            {
                current |= 0x80;
            }

            encoded.Add(current);
        }
        while (remaining != 0);

        return [.. encoded];
    }
}
