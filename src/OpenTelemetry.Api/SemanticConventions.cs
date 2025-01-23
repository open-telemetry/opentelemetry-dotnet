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
}
