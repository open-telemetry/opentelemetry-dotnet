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
        using var activity = ZipkinExporterTests.CreateTestActivity();

        // Act & Assert
        var zipkinSpan = ZipkinActivityConversionExtensions.ToZipkinSpan(activity, DefaultZipkinEndpoint);

        Assert.NotNull(zipkinSpan.RemoteEndpoint);
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

        public static IEnumerable<object[]> GetTestCases()
        {
            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 1: Only peer.service provided",
                    ExpectedResult = "PeerService",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["peer.service"] = "PeerService",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 2: Only server.address provided",
                    ExpectedResult = "ServerAddress",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["server.address"] = "ServerAddress",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 3: Only net.peer.name provided",
                    ExpectedResult = "NetPeerName",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["net.peer.name"] = "NetPeerName",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 4: network.peer.address and network.peer.port provided",
                    ExpectedResult = "1.2.3.4:5678",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["network.peer.address"] = "1.2.3.4",
                        ["network.peer.port"] = "5678",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 4: Only network.peer.address provided",
                    ExpectedResult = "1.2.3.4",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["network.peer.address"] = "1.2.3.4",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 5: Only server.socket.domain provided",
                    ExpectedResult = "SocketDomain",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["server.socket.domain"] = "SocketDomain",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 6: server.socket.address and server.socket.port provided",
                    ExpectedResult = "SocketAddress:4321",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["server.socket.address"] = "SocketAddress",
                        ["server.socket.port"] = "4321",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 7: Only net.sock.peer.name provided",
                    ExpectedResult = "NetSockPeerName",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["net.sock.peer.name"] = "NetSockPeerName",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 8: net.sock.peer.addr and net.sock.peer.port provided",
                    ExpectedResult = "5.6.7.8:8765",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["net.sock.peer.addr"] = "5.6.7.8",
                        ["net.sock.peer.port"] = "8765",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 9: Only peer.hostname provided",
                    ExpectedResult = "PeerHostname",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["peer.hostname"] = "PeerHostname",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 10: Only peer.address provided",
                    ExpectedResult = "PeerAddress",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["peer.address"] = "PeerAddress",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Rank 11: Only db.name provided",
                    ExpectedResult = "DbName",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["db.name"] = "DbName",
                    },
                },
            };

            yield return new object[]
            {
                new RemoteEndpointPriorityTestCase
                {
                    Name = "Multiple attributes: highest rank wins",
                    ExpectedResult = "PeerService",
                    RemoteEndpointAttributes = new Dictionary<string, object>
                    {
                        ["db.name"] = "DbName",
                        ["peer.address"] = "PeerAddress",
                        ["peer.hostname"] = "PeerHostname",
                        ["net.sock.peer.addr"] = "5.6.7.8",
                        ["net.sock.peer.port"] = "8765",
                        ["net.sock.peer.name"] = "NetSockPeerName",
                        ["server.socket.address"] = "SocketAddress",
                        ["server.socket.port"] = "4321",
                        ["server.socket.domain"] = "SocketDomain",
                        ["network.peer.address"] = "1.2.3.4",
                        ["network.peer.port"] = "5678",
                        ["net.peer.name"] = "NetPeerName",
                        ["server.address"] = "ServerAddress",
                        ["peer.service"] = "PeerService",
                    },
                },
            };
        }

        public override string? ToString()
        {
            return this.Name;
        }
    }
}
