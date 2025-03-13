// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Zipkin.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

public class ZipkinActivityExporterRemoteEndpointTests
{
    private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new("TestService");

    [Fact]
    public void GenerateActivity_RemoteEndpointOmittedByDefault()
    {
        // Arrange
        using var activity = ZipkinActivitySource.CreateTestActivity();

        // Act & Assert
        var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

        Assert.NotNull(zipkinSpan.RemoteEndpoint);
    }

    [Fact]
    public void GenerateActivity_RemoteEndpointResolution()
    {
        // Arrange
        using var activity = ZipkinActivitySource.CreateTestActivity(
            additionalAttributes: new Dictionary<string, object> { ["net.peer.name"] = "RemoteServiceName", });

        // Act & Assert
        var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

        Assert.NotNull(zipkinSpan.RemoteEndpoint);
        Assert.Equal("RemoteServiceName", zipkinSpan.RemoteEndpoint.ServiceName);
    }

    [Theory]
    [MemberData(nameof(RemoteEndpointPriorityTestCase.TestCases), MemberType = typeof(RemoteEndpointPriorityTestCase))]
    public void GenerateActivity_RemoteEndpointResolutionPriority(RemoteEndpointPriorityTestCase testCase)
    {
        // Arrange
        using var activity =
            ZipkinActivitySource.CreateTestActivity(additionalAttributes: testCase!.RemoteEndpointAttributes!);

        // Act & Assert
        var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

        Assert.NotNull(zipkinSpan.RemoteEndpoint);
        Assert.Equal(testCase.ExpectedResult, zipkinSpan.RemoteEndpoint.ServiceName);
    }
}
