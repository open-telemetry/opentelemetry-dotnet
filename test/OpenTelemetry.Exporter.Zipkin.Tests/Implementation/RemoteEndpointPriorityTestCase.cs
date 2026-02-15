// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

#pragma warning disable CA1515 // Consider making public types internal
public class RemoteEndpointPriorityTestCase
#pragma warning restore CA1515 // Consider making public types internal
{
#pragma warning disable CA1825 // HACK Workaround for https://github.com/dotnet/sdk/issues/53047
    public static TheoryData<RemoteEndpointPriorityTestCase> TestCases =>
    [
        new()
        {
            Name = "Rank 1: Only peer.service provided",
            ExpectedResult = "PeerService",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributePeerService] = "PeerService",
            },
        },
        new()
        {
            Name = "Rank 2: Only server.address provided",
            ExpectedResult = "ServerAddress",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeServerAddress] = "ServerAddress",
            },
        },
        new()
        {
            Name = "Rank 3: Only net.peer.name provided",
            ExpectedResult = "NetPeerName",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeNetPeerName] = "NetPeerName",
            },
        },
        new()
        {
            Name = "Rank 4: network.peer.address and network.peer.port provided",
            ExpectedResult = "1.2.3.4:5678",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeNetworkPeerAddress] = "1.2.3.4",
                [SemanticConventions.AttributeNetworkPeerPort] = "5678",
            },
        },
        new()
        {
            Name = "Rank 4: Only network.peer.address provided",
            ExpectedResult = "1.2.3.4",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeNetworkPeerAddress] = "1.2.3.4",
            },
        },
        new()
        {
            Name = "Rank 5: Only server.socket.domain provided",
            ExpectedResult = "SocketDomain",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeServerSocketDomain] = "SocketDomain",
            },
        },
        new()
        {
            Name = "Rank 6: server.socket.address and server.socket.port provided",
            ExpectedResult = "SocketAddress:4321",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeServerSocketAddress] = "SocketAddress",
                [SemanticConventions.AttributeServerSocketPort] = "4321",
            },
        },
        new()
        {
            Name = "Rank 7: Only net.sock.peer.name provided",
            ExpectedResult = "NetSockPeerName",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeNetSockPeerName] = "NetSockPeerName",
            },
        },
        new()
        {
            Name = "Rank 8: net.sock.peer.addr and net.sock.peer.port provided",
            ExpectedResult = "5.6.7.8:8765",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeNetSockPeerAddr] = "5.6.7.8",
                [SemanticConventions.AttributeNetSockPeerPort] = "8765",
            },
        },
        new()
        {
            Name = "Rank 9: Only peer.hostname provided",
            ExpectedResult = "PeerHostname",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributePeerHostname] = "PeerHostname",
            },
        },
        new()
        {
            Name = "Rank 10: Only peer.address provided",
            ExpectedResult = "PeerAddress",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributePeerAddress] = "PeerAddress",
            },
        },
        new()
        {
            Name = "Rank 11: Only db.name provided",
            ExpectedResult = "DbName",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeDbName] = "DbName",
            },
        },
        new()
        {
            Name = "Multiple attributes: highest rank wins",
            ExpectedResult = "PeerService",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                [SemanticConventions.AttributeDbName] = "DbName",
                [SemanticConventions.AttributePeerAddress] = "PeerAddress",
                [SemanticConventions.AttributePeerHostname] = "PeerHostname",
                [SemanticConventions.AttributeNetSockPeerAddr] = "5.6.7.8",
                [SemanticConventions.AttributeNetSockPeerPort] = "8765",
                [SemanticConventions.AttributeNetSockPeerName] = "NetSockPeerName",
                [SemanticConventions.AttributeServerSocketAddress] = "SocketAddress",
                [SemanticConventions.AttributeServerSocketPort] = "4321",
                [SemanticConventions.AttributeServerSocketDomain] = "SocketDomain",
                [SemanticConventions.AttributeNetworkPeerAddress] = "1.2.3.4",
                [SemanticConventions.AttributeNetworkPeerPort] = "5678",
                [SemanticConventions.AttributeNetPeerName] = "NetPeerName",
                [SemanticConventions.AttributeServerAddress] = "ServerAddress",
                [SemanticConventions.AttributePeerService] = "PeerService",
            },
        },
    ];
#pragma warning restore CA1825

    public string? Name { get; private set; }

    public string? ExpectedResult { get; private set; }

    public Dictionary<string, object>? RemoteEndpointAttributes { get; private set; }

    public override string? ToString()
    {
        return this.Name;
    }
}
