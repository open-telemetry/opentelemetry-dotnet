// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
}
