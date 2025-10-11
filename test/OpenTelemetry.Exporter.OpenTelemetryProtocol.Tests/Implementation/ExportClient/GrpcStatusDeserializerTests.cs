// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

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
        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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
        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        byte[] data = Convert.FromBase64String(grpcStatusDetailsBin);
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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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
    {
        Assert.Null(GrpcStatusDeserializer.TryGetGrpcRetryDelay("invalid-base64"));
    }

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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

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

        string grpcStatusDetailsBin = Convert.ToBase64String(status.ToByteArray());

        // Act
        var result = GrpcStatusDeserializer.TryGetGrpcRetryDelay(grpcStatusDetailsBin);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TimeSpan.FromSeconds(0.5), result);
    }

    [Fact]
    public void DeserializeStatus_TruncatedStream_ThrowsEndOfStreamException()
    {
        // Arrange: Create valid Base64 data and truncate it
        var status = new Google.Rpc.Status
        {
            Code = 3,
            Message = "Truncated stream test",
        };

        byte[] fullData = status.ToByteArray();
        byte[] truncatedData = fullData.Take(fullData.Length / 2).ToArray(); // Truncate the data

        string grpcStatusDetailsBin = Convert.ToBase64String(truncatedData);

        // Act & Assert: Attempt to deserialize and expect an EndOfStreamException
        Assert.Throws<EndOfStreamException>(() =>
            GrpcStatusDeserializer.DeserializeStatus(grpcStatusDetailsBin));
    }
}
