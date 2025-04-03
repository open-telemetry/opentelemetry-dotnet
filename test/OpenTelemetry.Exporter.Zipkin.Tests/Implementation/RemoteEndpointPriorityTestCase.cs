// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

#pragma warning disable CA1515 // Consider making public types internal
public class RemoteEndpointPriorityTestCase
#pragma warning restore CA1515 // Consider making public types internal
{
    public static TheoryData<RemoteEndpointPriorityTestCase> TestCases =>
    [
        new()
        {
            Name = "Rank 1: Only peer.service provided",
            ExpectedResult = "PeerService",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["peer.service"] = "PeerService",
            },
        },
        new()
        {
            Name = "Rank 2: Only server.address provided",
            ExpectedResult = "ServerAddress",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["server.address"] = "ServerAddress",
            },
        },
        new()
        {
            Name = "Rank 3: Only net.peer.name provided",
            ExpectedResult = "NetPeerName",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["net.peer.name"] = "NetPeerName",
            },
        },
        new()
        {
            Name = "Rank 4: network.peer.address and network.peer.port provided",
            ExpectedResult = "1.2.3.4:5678",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["network.peer.address"] = "1.2.3.4",
                ["network.peer.port"] = "5678",
            },
        },
        new()
        {
            Name = "Rank 4: Only network.peer.address provided",
            ExpectedResult = "1.2.3.4",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["network.peer.address"] = "1.2.3.4",
            },
        },
        new()
        {
            Name = "Rank 5: Only server.socket.domain provided",
            ExpectedResult = "SocketDomain",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["server.socket.domain"] = "SocketDomain",
            },
        },
        new()
        {
            Name = "Rank 6: server.socket.address and server.socket.port provided",
            ExpectedResult = "SocketAddress:4321",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["server.socket.address"] = "SocketAddress",
                ["server.socket.port"] = "4321",
            },
        },
        new()
        {
            Name = "Rank 7: Only net.sock.peer.name provided",
            ExpectedResult = "NetSockPeerName",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["net.sock.peer.name"] = "NetSockPeerName",
            },
        },
        new()
        {
            Name = "Rank 8: net.sock.peer.addr and net.sock.peer.port provided",
            ExpectedResult = "5.6.7.8:8765",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["net.sock.peer.addr"] = "5.6.7.8",
                ["net.sock.peer.port"] = "8765",
            },
        },
        new()
        {
            Name = "Rank 9: Only peer.hostname provided",
            ExpectedResult = "PeerHostname",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["peer.hostname"] = "PeerHostname",
            },
        },
        new()
        {
            Name = "Rank 10: Only peer.address provided",
            ExpectedResult = "PeerAddress",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["peer.address"] = "PeerAddress",
            },
        },
        new()
        {
            Name = "Rank 11: Only db.name provided",
            ExpectedResult = "DbName",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["db.name"] = "DbName",
            },
        },
        new()
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
    ];

    public string? Name { get; private set; }

    public string? ExpectedResult { get; private set; }

    public Dictionary<string, object>? RemoteEndpointAttributes { get; private set; }

    public override string? ToString()
    {
        return this.Name;
    }
}
