// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Proto.Trace.V1;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpResourceTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ToOtlpResourceTest(bool includeServiceNameInResource, bool useCustomSerializer)
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

        if (useCustomSerializer)
        {
            byte[] buffer = new byte[1024];
            var writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, 0, resource);

            // Deserialize the ResourceSpans and validate the attributes.
            using (var stream = new MemoryStream(buffer, 0, writePosition))
            {
                var resourceSpans = ResourceSpans.Parser.ParseFrom(stream);
                otlpResource = resourceSpans.Resource;
            }
        }
        else
        {
            otlpResource = resource.ToOtlpResource();
        }

        if (includeServiceNameInResource)
        {
            Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service-name");
            Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns1");
        }
        else
        {
            Assert.Contains(otlpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString().Contains("unknown_service:"));
        }
    }
}
