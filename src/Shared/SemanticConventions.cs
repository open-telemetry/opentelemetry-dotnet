// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.
/// </summary>
internal static class SemanticConventions
{
    public const string AttributeNetPeerIp = "net.peer.ip";
    public const string AttributeNetPeerPort = "net.peer.port";
    public const string AttributeNetPeerName = "net.peer.name";

    public const string AttributePeerService = "peer.service";

    public const string AttributeHttpHost = "http.host";
    public const string AttributeDbInstance = "db.instance";

    public const string AttributeMessageType = "message.type";
    public const string AttributeMessageId = "message.id";

    public const string AttributeExceptionEventName = "exception";
    public const string AttributeExceptionType = "exception.type";
    public const string AttributeExceptionMessage = "exception.message";
    public const string AttributeExceptionStacktrace = "exception.stacktrace";

    public const string AttributeServerAddress = "server.address";

    public const string AttributeNetworkPeerAddress = "network.peer.address";
    public const string AttributeNetworkPeerPort = "network.peer.port";

    public const string AttributeServerSocketDomain = "server.socket.domain";
    public const string AttributeServerSocketAddress = "server.socket.address";
    public const string AttributeServerSocketPort = "server.socket.port";

    public const string AttributeNetSockPeerName = "net.sock.peer.name";
    public const string AttributeNetSockPeerAddr = "net.sock.peer.addr";
    public const string AttributeNetSockPeerPort = "net.sock.peer.port";

    public const string AttributePeerHostname = "peer.hostname";
    public const string AttributePeerAddress = "peer.address";

    public const string AttributeDbName = "db.name";
}
