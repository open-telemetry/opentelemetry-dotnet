// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Proto.Trace.V1;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpResourceTests
{
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

        byte[] buffer = new byte[1024];
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
}
