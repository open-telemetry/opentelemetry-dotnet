// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;
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
            Name = "Highest priority name = net.peer.name",
            ExpectedResult = "RemoteServiceName",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["http.host"] = "DiscardedRemoteServiceName",
                ["net.peer.name"] = "RemoteServiceName",
                ["peer.hostname"] = "DiscardedRemoteServiceName",
            },
        },
        new()
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
        new()
        {
            Name = "Only has net.peer.name and net.peer.port",
            ExpectedResult = "RemoteServiceName:1234",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["net.peer.name"] = "RemoteServiceName",
                ["net.peer.port"] = "1234",
            },
        },
        new()
        {
            Name = "net.peer.port is an int",
            ExpectedResult = "RemoteServiceName:1234",
            RemoteEndpointAttributes = new Dictionary<string, object>
            {
                ["net.peer.name"] = "RemoteServiceName",
                ["net.peer.port"] = 1234,
            },
        },
        new()
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
        new()
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
        new()
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

    public string? Name { get; private set; }

    public string? ExpectedResult { get; private set; }

    public Dictionary<string, object>? RemoteEndpointAttributes { get; private set; }

    public override string? ToString()
    {
        return this.Name;
    }
}
