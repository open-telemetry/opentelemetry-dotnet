// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpExportProtocolParserTests
{
    [Theory]
#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
    [InlineData("grpc", true, OtlpExportProtocol.Grpc)]
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
    [InlineData("http/protobuf", true, OtlpExportProtocol.HttpProtobuf)]
    [InlineData("unsupported", false, default(OtlpExportProtocol))]
    public void TryParse_Protocol_MapsToCorrectValue(string protocol, bool expectedResult, OtlpExportProtocol expectedExportProtocol)
    {
        var result = OtlpExportProtocolParser.TryParse(protocol, out var exportProtocol);

        Assert.Equal(expectedExportProtocol, exportProtocol);
        Assert.Equal(expectedResult, result);
    }
}
