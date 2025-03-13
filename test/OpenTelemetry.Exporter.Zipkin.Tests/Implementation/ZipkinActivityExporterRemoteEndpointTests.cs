// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Zipkin.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

public class ZipkinActivityExporterRemoteEndpointTests
{
    private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new("TestService");

    [Fact]
    public void GenerateActivity_RemoteEndpointOmittedByDefault()
    {
        // Arrange
        using var activity = ZipkinExporterTests.CreateTestActivity();

        // Act & Assert
        var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

        Assert.NotNull(zipkinSpan.RemoteEndpoint);
    }

    [Fact]
    public void GenerateActivity_RemoteEndpointResolution()
    {
        // Arrange
        using var activity = ZipkinExporterTests.CreateTestActivity(
            additionalAttributes: new Dictionary<string, object>
            {
                ["net.peer.name"] = "RemoteServiceName",
            });

        // Act & Assert
        var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

        Assert.NotNull(zipkinSpan.RemoteEndpoint);
        Assert.Equal("RemoteServiceName", zipkinSpan.RemoteEndpoint.ServiceName);
    }

    [Theory]
    [MemberData(nameof(RemoteEndpointPriorityTestCase.GetTestCases), MemberType = typeof(RemoteEndpointPriorityTestCase))]
    public void GenerateActivity_RemoteEndpointResolutionPriority(RemoteEndpointPriorityTestCase testCase)
    {
        // Arrange
        using var activity = ZipkinExporterTests.CreateTestActivity(additionalAttributes: testCase.RemoteEndpointAttributes!);

        // Act & Assert
        var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

        Assert.NotNull(zipkinSpan.RemoteEndpoint);
        Assert.Equal(testCase.ExpectedResult, zipkinSpan.RemoteEndpoint.ServiceName);
    }

    public class RemoteEndpointPriorityTestCase
    {
        public string? Name { get; set; }

        public string? ExpectedResult { get; set; }

        public Dictionary<string, object>? RemoteEndpointAttributes { get; set; }

        public static TheoryData<RemoteEndpointPriorityTestCase> GetTestCases()
        {
            return
            [
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Highest priority name = net.peer.name",
                    ExpectedResult = "RemoteServiceName",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.name"] = "RemoteServiceName",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Highest priority name = SemanticConventions.AttributePeerService",
                    ExpectedResult = "RemoteServiceName",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        [SemanticConventions.AttributePeerService] = "RemoteServiceName",
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.name"] = "DiscardedRemoteServiceName",
                        ["net.peer.port"] = "1234",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Only has net.peer.name and net.peer.port",
                    ExpectedResult = "RemoteServiceName:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["net.peer.name"] = "RemoteServiceName",
                        ["net.peer.port"] = "1234",
                    },
                },
                new RemoteEndpointPriorityTestCase
                {
                    Name = "net.peer.port is an int",
                    ExpectedResult = "RemoteServiceName:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["net.peer.name"] = "RemoteServiceName",
                        ["net.peer.port"] = 1234,
                    },
                },
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Has net.peer.name and net.peer.port",
                    ExpectedResult = "RemoteServiceName:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.name"] = "RemoteServiceName",
                        ["net.peer.port"] = "1234",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Has net.peer.ip and net.peer.port",
                    ExpectedResult = "1.2.3.4:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.ip"] = "1.2.3.4",
                        ["net.peer.port"] = "1234",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Has net.peer.name, net.peer.ip, and net.peer.port",
                    ExpectedResult = "RemoteServiceName:1234",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["http.host"] = "DiscardedRemoteServiceName",
                        ["net.peer.name"] = "RemoteServiceName",
                        ["net.peer.ip"] = "1.2.3.4",
                        ["net.peer.port"] = "1234",
                        ["peer.hostname"] = "DiscardedRemoteServiceName",
                    },
                },
            ];
        }

        public override string? ToString()
        {
            return this.Name;
        }
    }
}
