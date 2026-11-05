// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Runtime.CompilerServices;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Proto.Trace.V1;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpResourceTests
{
    [Fact]
    public void EmptyResourceSerializesToExpectedBytes()
    {
        var buffer = new byte[5];
        var writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, 0, Resource.Empty);

        Assert.Equal(5, writePosition);
        Assert.Equal([0x0A, 0x80, 0x80, 0x80, 0x00], buffer);
    }

    [Fact]
    public void NullResourceSerializesToExpectedBytes()
    {
        var buffer = new byte[5];
        var writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, 0, resource: null);

        Assert.Equal(5, writePosition);
        Assert.Equal([0x0A, 0x80, 0x80, 0x80, 0x00], buffer);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToOtlpResourceTest(bool includeServiceNameInResource)
    {
        // Targeted test to cover OTel Resource to OTLP Resource
        // conversion, independent of signals.
        var resourceBuilder = ResourceBuilder.CreateEmpty();
        if (includeServiceNameInResource)
        {
            resourceBuilder.AddService("service-name", "ns1");
        }

        var resource = resourceBuilder.Build();
        Proto.Resource.V1.Resource otlpResource;

        var buffer = new byte[1024];
        var writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, 0, resource);

        // Deserialize the ResourceSpans and validate the attributes.
        using (var stream = new MemoryStream(buffer, 0, writePosition))
        {
            var resourceSpans = ResourceSpans.Parser.ParseFrom(stream);
            otlpResource = resourceSpans.Resource;
        }

        if (includeServiceNameInResource)
        {
            Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service-name");
            Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns1");
        }
        else
        {
            Assert.DoesNotContain(otlpResource.Attributes, kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName);
        }
    }

    [Fact]
    public void WriteResourceDoesNotKeepResourceAlive()
    {
        var reference = CreateSerializedResourceWeakReference();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.TryGetTarget(out _), "Resource should not be kept alive after serialization.");
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(4096)]
    public void SerializeResourceToBytesRejectsResourceExceedingLimit(int maxBufferSize)
    {
        var resource = ResourceBuilder.CreateEmpty()
            .AddAttributes([new("key", new string('x', maxBufferSize))])
            .Build();
        var pool = new TrackingArrayPool();

        Assert.Throws<InvalidOperationException>(() =>
            ProtobufOtlpResourceSerializer.SerializeResourceToBytes(resource, pool, maxBufferSize));

        Assert.All(pool.RequestedSizes, size => Assert.InRange(size, 1, maxBufferSize));
        Assert.Equal(pool.Rented, pool.Returned);
        Assert.All(pool.Returned, static array => Assert.All(array, static value => Assert.Equal(0, value)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2000)]
    [InlineData(3000)]
    [InlineData(4077)]
    public void SerializeResourceToBytesPreservesResourceWithinLimit(int valueLength)
    {
        const int MaxBufferSize = 4096;
        var value = new string('x', valueLength);
        var resource = ResourceBuilder.CreateEmpty()
            .AddAttributes([new("key", value)])
            .Build();
        var pool = new TrackingArrayPool();

        var serialized = ProtobufOtlpResourceSerializer.SerializeResourceToBytes(resource, pool, MaxBufferSize);

        var expected = new Proto.Resource.V1.Resource
        {
            Attributes =
            {
                new Proto.Common.V1.KeyValue
                {
                    Key = "key",
                    Value = new Proto.Common.V1.AnyValue { StringValue = value },
                },
            },
        };
        Assert.Equal(expected, ResourceSpans.Parser.ParseFrom(serialized).Resource);
        Assert.InRange(serialized.Length, 1, MaxBufferSize);
        Assert.All(pool.RequestedSizes, size => Assert.InRange(size, 1, MaxBufferSize));
        Assert.Equal(pool.Rented, pool.Returned);
        Assert.All(pool.Returned, static array => Assert.All(array, static value => Assert.Equal(0, value)));
    }

    [Fact]
    public void SerializeResourceToBytesRejectsResourceExceedingLimitWithOversizedRental()
    {
        var resource = ResourceBuilder.CreateEmpty()
            .AddAttributes([new("key", new string('x', 3000))])
            .Build();
        var pool = new TrackingArrayPool(minimumBufferSize: 4096);

        Assert.Throws<InvalidOperationException>(() =>
            ProtobufOtlpResourceSerializer.SerializeResourceToBytes(resource, pool, 2048));

        Assert.Single(pool.Rented);
        Assert.Equal(pool.Rented, pool.Returned);
        Assert.All(pool.Returned, static array => Assert.All(array, static value => Assert.Equal(0, value)));
    }

    [Fact]
    public void SerializeResourceToBytesReturnsBufferOnceWhenGrowthFails()
    {
        var resource = ResourceBuilder.CreateEmpty()
            .AddAttributes([new("key", new string('x', 3000))])
            .Build();
        var allocationFailure = new InvalidOperationException("Simulated buffer rental failure.");
        var pool = new TrackingArrayPool(allocationFailure);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProtobufOtlpResourceSerializer.SerializeResourceToBytes(resource, pool, 4096));

        Assert.Same(allocationFailure, exception);
        Assert.Equal(pool.Rented, pool.Returned);
        Assert.All(pool.Returned, static array => Assert.All(array, static value => Assert.Equal(0, value)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Resource> CreateSerializedResourceWeakReference()
    {
        var resource = ResourceBuilder.CreateEmpty()
            .AddAttributes([new("key", "value")])
            .Build();

        var buffer = new byte[1024];

        _ = ProtobufOtlpResourceSerializer.WriteResource(buffer, 0, resource);

        return new WeakReference<Resource>(resource);
    }

    private sealed class TrackingArrayPool(
        InvalidOperationException? growthFailure = null,
        int minimumBufferSize = 0) : ArrayPool<byte>
    {
        public List<int> RequestedSizes { get; } = [];

        public List<byte[]> Rented { get; } = [];

        public List<byte[]> Returned { get; } = [];

        public override byte[] Rent(int minimumLength)
        {
            this.RequestedSizes.Add(minimumLength);
            if (this.Rented.Count > 0 && growthFailure != null)
            {
                throw growthFailure;
            }

            var buffer = new byte[Math.Max(minimumLength, minimumBufferSize)];
            this.Rented.Add(buffer);
            return buffer;
        }

        public override void Return(byte[] array, bool clearArray = false)
        {
            if (clearArray)
            {
                array.AsSpan().Clear();
            }

            this.Returned.Add(array);
        }
    }
}
