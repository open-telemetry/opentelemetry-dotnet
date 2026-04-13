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
        Assert.Single(deserializedStatus.Value.Details);
        Assert.Equal("type.googleapis.com/google.protobuf.StringValue", deserializedStatus.Value.Details[0].TypeUrl);
        var stringValue = StringValue.Parser.ParseFrom(deserializedStatus.Value.Details[0].Value);
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
