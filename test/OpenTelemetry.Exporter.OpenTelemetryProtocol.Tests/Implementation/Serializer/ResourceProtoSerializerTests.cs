// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Proto.Trace.V1;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.Serializer;

public class ResourceProtoSerializerTests
{
    [Fact]
    public void CreateResource_SupportedAttributeTypes()
    {
        // Arrange
        byte[] buffer = new byte[1024];
        var attributes = new Dictionary<string, object>
        {
            { "string", "stringValue" },
            { "bool", true },
            { "double", 0.1d },
            { "long", 1L },

            // int and float supported by conversion to long and double
            { "int", 1 },
            { "short", (short)1 },
            { "float", 0.1f },

            // natively supported array types
            { "string arr", new string[] { "stringValue1", "stringValue2" } },
            { "bool arr", new bool[] { true } },
            { "double arr", new double[] { 0.1d } },
            { "long arr", new long[] { 1L } },

            // have to convert to other primitive array types
            { "int arr", new int[] { 1, 2, 3 } },
            { "short arr", new short[] { (short)1 } },
            { "float arr", new float[] { 0.1f } },
        };

        // Act
        var resource = ResourceBuilder.CreateEmpty().AddAttributes(attributes).Build();
        var writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, 0, resource);
        var otlpResource = resource.ToOtlpResource();
        var expectedResourceSpans = new ResourceSpans
        {
            Resource = otlpResource,
        };

        // Deserialize the ResourceSpans and validate the attributes.
        ResourceSpans actualResourceSpans;
        using (var stream = new MemoryStream(buffer, 0, writePosition))
        {
            actualResourceSpans = ResourceSpans.Parser.ParseFrom(stream);
        }

        // Assert
        Assert.Equal(expectedResourceSpans.Resource.Attributes.Count, actualResourceSpans.Resource.Attributes.Count);
        foreach (var actualAttribute in actualResourceSpans.Resource.Attributes)
        {
            Assert.Contains(actualAttribute, expectedResourceSpans.Resource.Attributes);
        }
    }
}
