// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Zipkin.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

public class ZipkinActivityExporterRemoteEndpointTests
{
    private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new("TestService");

    [Theory]
    [MemberData(nameof(RemoteEndpointPriorityTestCase.TestCases), MemberType = typeof(RemoteEndpointPriorityTestCase))]
    public void GenerateActivity_RemoteEndpointResolutionPriority(RemoteEndpointPriorityTestCase testCase)
    {
#if NET
        Assert.NotNull(testCase);
#else
        if (testCase == null)
        {
            throw new ArgumentNullException(nameof(testCase));
        }
#endif

        // Arrange
        using var activity =
            ZipkinActivitySource.CreateTestActivity(additionalAttributes: testCase.RemoteEndpointAttributes!);

        // Act & Assert
        var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

        Assert.NotNull(zipkinSpan.RemoteEndpoint);
        Assert.Equal(testCase.ExpectedResult, zipkinSpan.RemoteEndpoint.ServiceName);
    }

    [Fact]
    public void GenerateActivity_RemoteEndpointCacheIsBounded()
    {
        ZipkinActivityConversionExtensions.ClearRemoteEndpointCache();

        for (var i = 0; i < ZipkinActivityConversionExtensions.MaxRemoteEndpointCacheSize + 200; i++)
        {
            using var activity = ZipkinActivitySource.CreateTestActivity(
                additionalAttributes: new Dictionary<string, object>
                {
                    [SemanticConventions.AttributePeerService] = $"service-{i}",
                });

            var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);
            Assert.Equal($"service-{i}", zipkinSpan.RemoteEndpoint?.ServiceName);
        }

        Assert.Equal(
            ZipkinActivityConversionExtensions.MaxRemoteEndpointCacheSize,
            ZipkinActivityConversionExtensions.GetRemoteEndpointCacheCount());
    }

    [Fact]
    public void GenerateActivity_RemoteEndpointCacheEvictsLeastRecentlyUsedEntry()
    {
        ZipkinActivityConversionExtensions.ClearRemoteEndpointCache();

        ZipkinEndpoint? firstEndpoint = null;

        for (var i = 0; i < ZipkinActivityConversionExtensions.MaxRemoteEndpointCacheSize; i++)
        {
            using var activity = ZipkinActivitySource.CreateTestActivity(
                additionalAttributes: new Dictionary<string, object>
                {
                    [SemanticConventions.AttributePeerService] = $"service-{i}",
                });

            var zipkinSpan = activity.ToZipkinSpan(DefaultZipkinEndpoint);
            if (i == 0)
            {
                firstEndpoint = zipkinSpan.RemoteEndpoint;
            }
        }

        using var overflowActivity = ZipkinActivitySource.CreateTestActivity(
            additionalAttributes: new Dictionary<string, object>
            {
                [SemanticConventions.AttributePeerService] = "service-overflow",
            });
        _ = overflowActivity.ToZipkinSpan(DefaultZipkinEndpoint);

        using var evictedEntryActivity = ZipkinActivitySource.CreateTestActivity(
            additionalAttributes: new Dictionary<string, object>
            {
                [SemanticConventions.AttributePeerService] = "service-0",
            });
        var evictedEntrySpan = evictedEntryActivity.ToZipkinSpan(DefaultZipkinEndpoint);

        Assert.NotNull(firstEndpoint);
        Assert.NotNull(evictedEntrySpan.RemoteEndpoint);
        Assert.NotSame(firstEndpoint, evictedEntrySpan.RemoteEndpoint);
        Assert.Equal(
            ZipkinActivityConversionExtensions.MaxRemoteEndpointCacheSize,
            ZipkinActivityConversionExtensions.GetRemoteEndpointCacheCount());
    }
}
