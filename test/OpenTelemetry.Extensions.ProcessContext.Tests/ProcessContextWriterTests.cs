// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Extensions.ProcessContext.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Extensions.ProcessContext.Tests;

public sealed class ProcessContextWriterTests : IDisposable
{
    private readonly FakeProcessContextPlatform platform = new();
    private readonly ProcessContextWriter writer;

    public ProcessContextWriterTests()
    {
        this.writer = new ProcessContextWriter(this.platform);
    }

    [Fact]
    public void PublishFollowsThePublicationProtocol()
    {
        var payload = Payload("svc");

        Assert.True(this.writer.Publish(payload));

        // Allocate, prevent fork inheritance, then name the mapping last.
        Assert.Equal(["CreateMapping", "Madvise", "NameMapping"], this.platform.Calls);

        var header = HeaderReader.Read(this.writer.MappingAddress);
        Assert.Equal("OTEL_CTX", header.Signature);
        Assert.Equal(2u, header.Version);
        Assert.Equal(1_000UL, header.PublishedAt);
        Assert.Equal(payload, header.ReadPayloadBytes());
        Assert.Equal("svc", header.DecodePayload().Resource.Attributes[0].Value.StringValue);
    }

    [Fact]
    public void UpdateZeroesTimestampWhileMutatingAndPublishesNewTimestamp()
    {
        this.platform.ClockValues.Clear();
        this.platform.ClockValues.Enqueue(1_000);
        this.platform.ClockValues.Enqueue(2_000);
        Assert.True(this.writer.Publish(Payload("v1")));
        var mapping = this.writer.MappingAddress;

        ulong? timestampWhileMutating = null;
        this.writer.OnMutatingForTesting = () => timestampWhileMutating = HeaderReader.Read(mapping).PublishedAt;

        Assert.True(this.writer.Publish(Payload("v2")));

        Assert.Equal(0UL, timestampWhileMutating);
        var header = HeaderReader.Read(mapping);
        Assert.Equal(2_000UL, header.PublishedAt);
        Assert.Equal("v2", header.DecodePayload().Resource.Attributes[0].Value.StringValue);

        // The same mapping is reused, renamed, and the old payload freed.
        Assert.Equal(mapping, this.writer.MappingAddress);
        Assert.Equal(["CreateMapping", "Madvise", "NameMapping", "NameMapping"], this.platform.Calls);
        Assert.Single(this.platform.LivePayloads);
    }

    [Fact]
    public void EveryUpdateGetsADifferentLaterTimestampEvenIfTheClockStalls()
    {
        this.platform.ClockValues.Clear();
        this.platform.ClockValues.Enqueue(5_000);

        var timestamps = new List<ulong>();
        for (var i = 0; i < 5; i++)
        {
            Assert.True(this.writer.Publish(Payload($"v{i}")));
            timestamps.Add(HeaderReader.Read(this.writer.MappingAddress).PublishedAt);
        }

        Assert.Equal([5_000UL, 5_001UL, 5_002UL, 5_003UL, 5_004UL], timestamps);
    }

    [Fact]
    public void PublishingTheSamePayloadDoesNotUpdate()
    {
        Assert.True(this.writer.Publish(Payload("svc")));
        var before = HeaderReader.Read(this.writer.MappingAddress);

        Assert.True(this.writer.Publish(Payload("svc")));

        Assert.Equal(before, HeaderReader.Read(this.writer.MappingAddress));
        Assert.Equal(["CreateMapping", "Madvise", "NameMapping"], this.platform.Calls);
    }

    [Fact]
    public void MemfdMappingIsKeptWhenNamingFails()
    {
        this.platform.NamingFails = true;

        Assert.True(this.writer.Publish(Payload("svc")));

        // Found as /memfd:OTEL_CTX even without a name.
        Assert.NotEqual(0, this.writer.MappingAddress);
        Assert.Single(this.platform.LiveMappings);
    }

    [Fact]
    public void AnonymousMappingIsKeptWhenNamingSucceeds()
    {
        this.platform.MemfdAvailable = false;

        Assert.True(this.writer.Publish(Payload("svc")));

        Assert.NotEqual(0, this.writer.MappingAddress);
    }

    [Fact]
    public void AnonymousMappingIsReleasedWhenNamingFails()
    {
        this.platform.MemfdAvailable = false;
        this.platform.NamingFails = true;

        Assert.False(this.writer.Publish(Payload("svc")));

        Assert.Equal(0, this.writer.MappingAddress);
        Assert.Empty(this.platform.LiveMappings);
        Assert.Empty(this.platform.LivePayloads);
        Assert.Equal(["CreateMapping", "Madvise", "NameMapping", "ReleaseMapping"], this.platform.Calls);
    }

    [Fact]
    public void MadviseFailureFailsPublication()
    {
        this.platform.MadviseFails = true;

        Assert.False(this.writer.Publish(Payload("svc")));

        Assert.Equal(0, this.writer.MappingAddress);
        Assert.Empty(this.platform.LiveMappings);
        Assert.Equal(["CreateMapping", "Madvise", "ReleaseMapping"], this.platform.Calls);
    }

    [Fact]
    public void MappingFailureFailsPublication()
    {
        this.platform.MappingFails = true;

        Assert.False(this.writer.Publish(Payload("svc")));
        Assert.Equal(0, this.writer.MappingAddress);
    }

    [Fact]
    public void ClockFailureFailsPublication()
    {
        this.platform.ClockValues.Clear();
        this.platform.ClockValues.Enqueue(0);

        Assert.False(this.writer.Publish(Payload("svc")));
        Assert.Empty(this.platform.Calls);
    }

    [Fact]
    public void OversizedPayloadIsRejected()
    {
        Assert.False(this.writer.Publish(new byte[ProcessContextWriter.MaxPayloadSize + 1]));
        Assert.False(this.writer.Publish([]));
        Assert.Empty(this.platform.Calls);
    }

    [Fact]
    public void DropInvalidatesAndReleases()
    {
        Assert.True(this.writer.Publish(Payload("svc")));

        this.writer.Drop();

        Assert.Equal(0, this.writer.MappingAddress);
        Assert.Empty(this.platform.LiveMappings);
        Assert.Empty(this.platform.LivePayloads);
        Assert.Equal("ReleaseMapping", this.platform.Calls[^1]);
    }

    [Fact]
    public void PublishAfterDropUsesANewMappingAndALaterTimestamp()
    {
        this.platform.ClockValues.Clear();
        this.platform.ClockValues.Enqueue(9_000);
        Assert.True(this.writer.Publish(Payload("v1")));
        this.writer.Drop();

        Assert.True(this.writer.Publish(Payload("v2")));

        Assert.Equal(9_001UL, HeaderReader.Read(this.writer.MappingAddress).PublishedAt);
    }

    [Fact]
    public void ForkedChildDoesNotTouchTheParentMapping()
    {
        Assert.True(this.writer.Publish(Payload("parent")));
        var parentMapping = this.writer.MappingAddress;
        var parentHeader = HeaderReader.Read(parentMapping);

        // Simulate fork: same memory image, different pid. MADV_DONTFORK means
        // the parent's mapping is absent in the child, so it must not be
        // written to or unmapped; a fresh one is created.
        this.platform.ProcessId = 5678;
        this.platform.Calls.Clear();

        Assert.True(this.writer.Publish(Payload("child")));

        Assert.NotEqual(parentMapping, this.writer.MappingAddress);
        Assert.Equal(["CreateMapping", "Madvise", "NameMapping"], this.platform.Calls);
        Assert.Equal(parentHeader, HeaderReader.Read(parentMapping));
        Assert.Contains(parentMapping, this.platform.LiveMappings);
    }

    [Fact]
    public void UnsupportedPlatformIsANoOp()
    {
        this.platform.IsSupported = false;

        Assert.False(this.writer.Publish(Payload("svc")));
        this.writer.Drop();

        Assert.Empty(this.platform.Calls);
    }

    public void Dispose() => this.platform.Dispose();

    internal static byte[] Payload(string serviceName)
        => ProcessContextSerializer.Serialize(
            new Resource(new Dictionary<string, object> { ["service.name"] = serviceName }),
            attributes: null);
}
