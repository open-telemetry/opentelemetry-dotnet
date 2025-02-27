// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Resources.Tests;

public class ResourceBuilderTests
{
    [Fact]
    public void ServiceResource_ServiceName()
    {
        var resource = ResourceBuilder.CreateEmpty().AddService("my-service").Build();
        Assert.Equal(2, resource.Attributes.Count());
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-service"), resource.Attributes);
        Assert.Single(resource.Attributes, kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceName);
        Assert.True(Guid.TryParse((string)resource.Attributes.Single(kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceInstance).Value, out _));
    }

    [Fact]
    public void ServiceResource_ServiceNameAndInstance()
    {
        var resource = ResourceBuilder.CreateEmpty().AddService("my-service", serviceInstanceId: "123").Build();
        Assert.Equal(2, resource.Attributes.Count());
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-service"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceInstance, "123"), resource.Attributes);
    }

    [Fact]
    public void ServiceResource_ServiceNameAndInstanceAndNamespace()
    {
        var resource = ResourceBuilder.CreateEmpty().AddService("my-service", "my-namespace", serviceInstanceId: "123").Build();
        Assert.Equal(3, resource.Attributes.Count());
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-service"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceInstance, "123"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceNamespace, "my-namespace"), resource.Attributes);
    }

    [Fact]
    public void ServiceResource_ServiceNameAndInstanceAndNamespaceAndVersion()
    {
        var resource = ResourceBuilder.CreateEmpty().AddService("my-service", "my-namespace", "1.2.3", serviceInstanceId: "123").Build();
        Assert.Equal(4, resource.Attributes.Count());
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-service"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceInstance, "123"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceNamespace, "my-namespace"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceVersion, "1.2.3"), resource.Attributes);
    }

    [Fact]
    public void ServiceResource_AutoGenerateServiceInstanceIdOff()
    {
        var resource = ResourceBuilder.CreateEmpty().AddService("my-service", autoGenerateServiceInstanceId: false).Build();
        Assert.Single(resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-service"), resource.Attributes);
    }

    [Fact]
    public void ServiceResourceGeneratesConsistentInstanceId()
    {
        var firstResource = ResourceBuilder.CreateEmpty().AddService("my-service").Build();

        var firstInstanceIdAttribute = firstResource.Attributes.FirstOrDefault(kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceInstance);

        Assert.NotNull(firstInstanceIdAttribute.Value);

        var secondResource = ResourceBuilder.CreateEmpty().AddService("other-service").Build();

        var secondInstanceIdAttribute = secondResource.Attributes.FirstOrDefault(kvp => kvp.Key == ResourceSemanticConventions.AttributeServiceInstance);

        Assert.NotNull(secondInstanceIdAttribute.Value);

        Assert.Equal(firstInstanceIdAttribute.Value, secondInstanceIdAttribute.Value);
    }

    [Fact]
    public void ClearTest()
    {
        var resource = ResourceBuilder.CreateEmpty()
            .AddTelemetrySdk()
            .Clear()
            .AddService("my-service", autoGenerateServiceInstanceId: false)
            .Build();
        Assert.Single(resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-service"), resource.Attributes);
    }
}
