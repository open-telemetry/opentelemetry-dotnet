// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Resources;
using Xunit;

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
        var otlpResource = resource.ToOtlpResource();
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