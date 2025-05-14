// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OpenTelemetry.Resources.Tests;

public sealed class ResourceTests : IDisposable
{
    private const string KeyName = "key";
    private const string ValueName = "value";

    public ResourceTests()
    {
        ClearEnvVars();
    }

    public void Dispose()
    {
        ClearEnvVars();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void CreateResource_NullAttributeCollection()
    {
        // Act and Assert
        var resource = new Resource(null!);
        Assert.Empty(resource.Attributes);
    }

    [Fact]
    public void CreateResource_NullAttributeValue()
    {
        // Arrange
        var attributes = new Dictionary<string, object?> { { "NullValue", null } };

        // Act and Assert
        Assert.Throws<ArgumentException>(() => new Resource(attributes!));
    }

    [Fact]
    public void CreateResource_EmptyAttributeKey()
    {
        // Arrange
        var attributes = new Dictionary<string, object> { { string.Empty, "value" } };

        // Act
        var resource = new Resource(attributes);

        // Assert
        Assert.Single(resource.Attributes);

        var attribute = resource.Attributes.Single();
        Assert.Empty(attribute.Key);
        Assert.Equal("value", attribute.Value);
    }

    [Fact]
    public void CreateResource_EmptyAttributeValue()
    {
        // Arrange
        var attributes = new Dictionary<string, object> { { "EmptyValue", string.Empty } };

        // does not throw
        var resource = new Resource(attributes);

        // Assert
        Assert.Single(resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("EmptyValue", string.Empty), resource.Attributes);
    }

    [Fact]
    public void CreateResource_EmptyArray()
    {
        // Arrange
        var attributes = new Dictionary<string, object> { { "EmptyArray", Array.Empty<string>() } };

        // does not throw
        var resource = new Resource(attributes);

        // Assert
        Assert.Single(resource.Attributes);
        Assert.Equal(Array.Empty<string>(), resource.Attributes.Where(x => x.Key == "EmptyArray").FirstOrDefault().Value);
    }

    [Fact]
    public void CreateResource_EmptyAttribute()
    {
        // Arrange
        var attributeCount = 0;
        var attributes = CreateAttributes(attributeCount);

        // Act
        var resource = new Resource(attributes);

        // Assert
        ValidateResource(resource, attributeCount);
    }

    [Fact]
    public void CreateResource_SingleAttribute()
    {
        // Arrange
        var attributeCount = 1;
        var attributes = CreateAttributes(attributeCount);

        // Act
        var resource = new Resource(attributes);

        // Assert
        ValidateResource(resource, attributeCount);
    }

    [Fact]
    public void CreateResource_MultipleAttribute()
    {
        // Arrange
        var attributeCount = 5;
        var attributes = CreateAttributes(attributeCount);

        // Act
        var resource = new Resource(attributes);

        // Assert
        ValidateResource(resource, attributeCount);
    }

    [Fact]
    public void CreateResource_SupportedAttributeTypes()
    {
        // Arrange
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
        };

        // Act
        var resource = new Resource(attributes);

        // Assert
        Assert.Equal(7, resource.Attributes.Count());
        Assert.Contains(new KeyValuePair<string, object>("string", "stringValue"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("bool", true), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("double", 0.1d), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("long", 1L), resource.Attributes);

        Assert.Contains(new KeyValuePair<string, object>("int", 1L), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("short", 1L), resource.Attributes);

        double convertedFloat = Convert.ToDouble(0.1f, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(new KeyValuePair<string, object>("float", convertedFloat), resource.Attributes);
    }

    [Fact]
    public void CreateResource_SupportedAttributeArrayTypes()
    {
        // Arrange
        string[] stringArray = ["stringValue"];
        bool[] boolArray = [true];
        double[] doubleArray = [0.1D];
        long[] longArray = [1L];
        int[] intArray = [1];
        short[] shortArray = [1];
        float[] floatArray = [0.1f];

        var attributes = new Dictionary<string, object>
        {
            // natively supported array types
            { "string arr", stringArray },
            { "bool arr", boolArray },
            { "double arr", doubleArray },
            { "long arr", longArray },

            // have to convert to other primitive array types
            { "int arr", intArray },
            { "short arr", shortArray },
            { "float arr", floatArray },
        };

        // Act
        var resource = new Resource(attributes);

        // Assert
        Assert.Equal(7, resource.Attributes.Count());
        Assert.Equal(stringArray, resource.Attributes.FirstOrDefault(x => x.Key == "string arr").Value);
        Assert.Equal(boolArray, resource.Attributes.FirstOrDefault(x => x.Key == "bool arr").Value);
        Assert.Equal(doubleArray, resource.Attributes.FirstOrDefault(x => x.Key == "double arr").Value);
        Assert.Equal(longArray, resource.Attributes.FirstOrDefault(x => x.Key == "long arr").Value);

        double[] nonNativeDoubleArray = [Convert.ToDouble(0.1f, System.Globalization.CultureInfo.InvariantCulture)];
        Assert.Equal(longArray, resource.Attributes.FirstOrDefault(x => x.Key == "int arr").Value);
        Assert.Equal(longArray, resource.Attributes.FirstOrDefault(x => x.Key == "short arr").Value);
        Assert.Equal(nonNativeDoubleArray, resource.Attributes.FirstOrDefault(x => x.Key == "float arr").Value);
    }

    [Fact]
    public void CreateResource_NotSupportedAttributeTypes()
    {
        var attributes = new Dictionary<string, object>
        {
            { "dynamic", new { } },
            { "array", new int[1] },
            { "complex", this },
        };

        Assert.Throws<ArgumentException>(() => new Resource(attributes));
    }

    [Fact]
    public void MergeResource_EmptyAttributeSource_MultiAttributeTarget()
    {
        // Arrange
        var sourceAttributeCount = 0;
        var sourceAttributes = CreateAttributes(sourceAttributeCount);
        var sourceResource = new Resource(sourceAttributes);

        var otherAttributeCount = 3;
        var otherAttributes = CreateAttributes(otherAttributeCount);
        var otherResource = new Resource(otherAttributes);

        // Act
        var newResource = sourceResource.Merge(otherResource);

        // Assert
        Assert.NotSame(otherResource, newResource);
        Assert.NotSame(sourceResource, newResource);

        ValidateResource(newResource, sourceAttributeCount + otherAttributeCount);
    }

    [Fact]
    public void MergeResource_MultiAttributeSource_EmptyAttributeTarget()
    {
        // Arrange
        var sourceAttributeCount = 3;
        var sourceAttributes = CreateAttributes(sourceAttributeCount);
        var sourceResource = new Resource(sourceAttributes);

        var otherAttributeCount = 0;
        var otherAttributes = CreateAttributes(otherAttributeCount);
        var otherResource = new Resource(otherAttributes);

        // Act
        var newResource = sourceResource.Merge(otherResource);

        // Assert
        Assert.NotSame(otherResource, newResource);
        Assert.NotSame(sourceResource, newResource);
        ValidateResource(newResource, sourceAttributeCount + otherAttributeCount);
    }

    [Fact]
    public void MergeResource_MultiAttributeSource_MultiAttributeTarget_NoOverlap()
    {
        // Arrange
        var sourceAttributeCount = 3;
        var sourceAttributes = CreateAttributes(sourceAttributeCount);
        var sourceResource = new Resource(sourceAttributes);

        var otherAttributeCount = 3;
        var otherAttributes = CreateAttributes(otherAttributeCount, sourceAttributeCount);
        var otherResource = new Resource(otherAttributes);

        // Act
        var newResource = sourceResource.Merge(otherResource);

        // Assert
        Assert.NotSame(otherResource, newResource);
        Assert.NotSame(sourceResource, newResource);
        ValidateResource(newResource, sourceAttributeCount + otherAttributeCount);
    }

    [Fact]
    public void MergeResource_MultiAttributeSource_MultiAttributeTarget_SingleOverlap()
    {
        // Arrange
        var sourceAttributeCount = 3;
        var sourceAttributes = CreateAttributes(sourceAttributeCount);
        var sourceResource = new Resource(sourceAttributes);

        var otherAttributeCount = 3;
        var otherAttributes = CreateAttributes(otherAttributeCount, sourceAttributeCount - 1);
        var otherResource = new Resource(otherAttributes);

        // Act
        var newResource = sourceResource.Merge(otherResource);

        // Assert
        Assert.NotSame(otherResource, newResource);
        Assert.NotSame(sourceResource, newResource);
        ValidateResource(newResource, sourceAttributeCount + otherAttributeCount - 1);

        // Also verify target attributes were not overwritten
        foreach (var otherAttribute in otherAttributes)
        {
            Assert.Contains(otherAttribute, otherResource.Attributes);
        }
    }

    [Fact]
    public void MergeResource_MultiAttributeSource_MultiAttributeTarget_FullOverlap()
    {
        // Arrange
        var sourceAttributeCount = 3;
        var sourceAttributes = CreateAttributes(sourceAttributeCount);
        var sourceResource = new Resource(sourceAttributes);

        var otherAttributeCount = 3;
        var otherAttributes = CreateAttributes(otherAttributeCount);
        var otherResource = new Resource(otherAttributes);

        // Act
        var newResource = sourceResource.Merge(otherResource);

        // Assert
        Assert.NotSame(otherResource, newResource);
        Assert.NotSame(sourceResource, newResource);
        ValidateResource(newResource, otherAttributeCount);

        // Also verify target attributes were not overwritten
        foreach (var otherAttribute in otherAttributes)
        {
            Assert.Contains(otherAttribute, otherResource.Attributes);
        }
    }

    [Fact]
    public void MergeResource_MultiAttributeSource_DuplicatedKeysInPrimary()
    {
        // Arrange
        var sourceAttributes = new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("key1", "value1"),
            new KeyValuePair<string, object>("key1", "value1.1"),
        };
        var sourceResource = new Resource(sourceAttributes);

        var otherAttributes = new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("key2", "value2"),
        };

        var otherResource = new Resource(otherAttributes);

        // Act
        var newResource = sourceResource.Merge(otherResource);

        // Assert
        Assert.NotSame(otherResource, newResource);
        Assert.NotSame(sourceResource, newResource);

        Assert.Equal(2, newResource.Attributes.Count());
        Assert.Contains(new KeyValuePair<string, object>("key1", "value1"), newResource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("key2", "value2"), newResource.Attributes);
    }

    [Fact]
    public void MergeResource_UpdatingResourceOverridesCurrentResource()
    {
        // Arrange
        var currentAttributes = new Dictionary<string, object> { { "value", "currentValue" } };
        var updatingAttributes = new Dictionary<string, object> { { "value", "updatedValue" } };
        var currentResource = new Resource(currentAttributes);
        var updatingResource = new Resource(updatingAttributes);

        var newResource = currentResource.Merge(updatingResource);

        // Assert
        Assert.Single(newResource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("value", "updatedValue"), newResource.Attributes);
    }

    [Fact]
    public void GetResourceWithTelemetrySDKAttributes()
    {
        // Arrange
        var resource = ResourceBuilder.CreateDefault().AddTelemetrySdk().Build();

        // Assert
        var attributes = resource.Attributes;
        Assert.Equal(4, attributes.Count());
        ValidateDefaultAttributes(attributes);
        ValidateTelemetrySdkAttributes(attributes);
    }

    [Fact]
    public void GetResourceWithDefaultAttributes_EmptyResource()
    {
        // Arrange
        var resource = ResourceBuilder.CreateDefault().Build();

        // Assert
        var attributes = resource.Attributes;
        Assert.Equal(4, attributes.Count());
        ValidateDefaultAttributes(attributes);
        ValidateTelemetrySdkAttributes(attributes);
    }

    [Fact]
    public void GetResourceWithDefaultAttributes_ResourceWithAttrs()
    {
        // Arrange
        var resource = ResourceBuilder.CreateDefault().AddAttributes(CreateAttributes(2)).Build();

        // Assert
        var attributes = resource.Attributes;
        Assert.Equal(6, attributes.Count());
        ValidateAttributes(attributes, 0, 1);
        ValidateDefaultAttributes(attributes);
        ValidateTelemetrySdkAttributes(attributes);
    }

    [Fact]
    public void GetResourceWithDefaultAttributes_WithResourceEnvVar()
    {
        // Arrange
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, "EVKey1=EVVal1,EVKey2=EVVal2");
        var resource = ResourceBuilder.CreateDefault().AddAttributes(CreateAttributes(2)).Build();

        // Assert
        var attributes = resource.Attributes;
        Assert.Equal(8, attributes.Count());
        ValidateAttributes(attributes, 0, 1);
        ValidateDefaultAttributes(attributes);
        Assert.Contains(new KeyValuePair<string, object>("EVKey1", "EVVal1"), attributes);
        Assert.Contains(new KeyValuePair<string, object>("EVKey2", "EVVal2"), attributes);
        ValidateTelemetrySdkAttributes(attributes);
    }

    [Fact]
    public void EnvironmentVariableDetectors_DoNotDuplicateAttributes()
    {
        // Arrange
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, "EVKey1=EVVal1,EVKey2=EVVal2");
        var resource = ResourceBuilder.CreateDefault().AddEnvironmentVariableDetector().AddEnvironmentVariableDetector().Build();

        // Assert
        var attributes = resource.Attributes;
        Assert.Equal(6, attributes.Count());
        Assert.Contains(new KeyValuePair<string, object>("EVKey1", "EVVal1"), attributes);
        Assert.Contains(new KeyValuePair<string, object>("EVKey2", "EVVal2"), attributes);
        ValidateTelemetrySdkAttributes(attributes);
    }

    [Fact]
    public void GetResource_WithServiceEnvVar()
    {
        // Arrange
        Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, "some-service");
        var resource = ResourceBuilder.CreateDefault().AddAttributes(CreateAttributes(2)).Build();

        // Assert
        var attributes = resource.Attributes;
        Assert.Equal(6, attributes.Count());
        ValidateAttributes(attributes, 0, 1);
        Assert.Contains(new KeyValuePair<string, object>("service.name", "some-service"), attributes);
        ValidateTelemetrySdkAttributes(attributes);
    }

    [Fact]
    public void GetResource_WithServiceNameSetWithTwoEnvVars()
    {
        // Arrange
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, "service.name=from-resource-attr");
        Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, "from-service-name");
        var resource = ResourceBuilder.CreateDefault().AddAttributes(CreateAttributes(2)).Build();

        // Assert
        var attributes = resource.Attributes;
        Assert.Equal(6, attributes.Count());
        ValidateAttributes(attributes, 0, 1);
        Assert.Contains(new KeyValuePair<string, object>("service.name", "from-service-name"), attributes);
        ValidateTelemetrySdkAttributes(attributes);
    }

    [Fact]
    public void GetResource_WithServiceNameSetWithTwoEnvVarsAndCode()
    {
        // Arrange
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, "service.name=from-resource-attr");
        Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, "from-service-name");
        var resource = ResourceBuilder.CreateDefault().AddService("from-code").AddAttributes(CreateAttributes(2)).Build();

        // Assert
        var attributes = resource.Attributes;
        Assert.Equal(7, attributes.Count());
        ValidateAttributes(attributes, 0, 1);
        Assert.Contains(new KeyValuePair<string, object>("service.name", "from-code"), attributes);
        ValidateTelemetrySdkAttributes(attributes);
    }

    [Fact]
    public void ResourceBuilder_AddDetector_Test()
    {
        bool factoryExecuted = false;

        var builder = ResourceBuilder.CreateDefault();

        builder.AddDetector(sp =>
        {
            factoryExecuted = true;
            return new NoopResourceDetector();
        });

        Assert.Throws<NotSupportedException>(() => builder.Build());
        Assert.False(factoryExecuted);

        var serviceCollection = new ServiceCollection();
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        builder.ServiceProvider = serviceProvider;

        var resource = builder.Build();

        Assert.True(factoryExecuted);
    }

    [Fact]
    public void ResourceBuilder_AddDetectorInternal_Test()
    {
        var builder = ResourceBuilder.CreateDefault();

        bool nullTestRun = false;

        builder.AddDetectorInternal(sp =>
        {
            nullTestRun = true;
            Assert.Null(sp);
            return new NoopResourceDetector();
        });

        builder.Build();

        Assert.True(nullTestRun);

        builder = ResourceBuilder.CreateDefault();

        bool validTestRun = false;

        var serviceCollection = new ServiceCollection();
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        builder.ServiceProvider = serviceProvider;

        builder.AddDetectorInternal(sp =>
        {
            validTestRun = true;
            Assert.NotNull(sp);
            return new NoopResourceDetector();
        });

        builder.Build();

        Assert.True(validTestRun);
    }

    internal static void ValidateTelemetrySdkAttributes(IEnumerable<KeyValuePair<string, object>> attributes)
    {
        Assert.Contains(new KeyValuePair<string, object>("telemetry.sdk.name", "opentelemetry"), attributes);
        Assert.Contains(new KeyValuePair<string, object>("telemetry.sdk.language", "dotnet"), attributes);
        var versionAttribute = attributes.Where(pair => pair.Key.Equals("telemetry.sdk.version", StringComparison.Ordinal));
        Assert.Single(versionAttribute);
    }

    internal static void ValidateDefaultAttributes(IEnumerable<KeyValuePair<string, object>> attributes)
    {
        var serviceName = attributes.Where(pair => pair.Key.Equals("service.name", StringComparison.Ordinal));
        Assert.Single(serviceName);
        Assert.Contains("unknown_service", serviceName.FirstOrDefault().Value as string, StringComparison.Ordinal);
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, null);
        Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, null);
    }

    private static void AddAttributes(Dictionary<string, object> attributes, int attributeCount, int startIndex = 0)
    {
        for (var i = startIndex; i < attributeCount + startIndex; ++i)
        {
            attributes.Add($"{KeyName}{i}", $"{ValueName}{i}");
        }
    }

    private static void ValidateAttributes(IEnumerable<KeyValuePair<string, object>> attributes, int startIndex = 0, int endIndex = 0)
    {
        var keyValuePairs = attributes as KeyValuePair<string, object>[] ?? attributes.ToArray();
        var endInd = endIndex == 0 ? keyValuePairs.Length - 1 : endIndex;
        for (var i = startIndex; i <= endInd; ++i)
        {
            Assert.Contains(new KeyValuePair<string, object>($"{KeyName}{i}", $"{ValueName}{i}"), keyValuePairs);
        }
    }

    private static void ValidateResource(Resource resource, int attributeCount)
    {
        Assert.NotNull(resource);
        Assert.NotNull(resource.Attributes);
        Assert.Equal(attributeCount, resource.Attributes.Count());
        ValidateAttributes(resource.Attributes);
    }

    private static Dictionary<string, object> CreateAttributes(int attributeCount, int startIndex = 0)
    {
        var attributes = new Dictionary<string, object>();
        AddAttributes(attributes, attributeCount, startIndex);
        return attributes;
    }

    private sealed class NoopResourceDetector : IResourceDetector
    {
        public Resource Detect() => Resource.Empty;
    }
}
