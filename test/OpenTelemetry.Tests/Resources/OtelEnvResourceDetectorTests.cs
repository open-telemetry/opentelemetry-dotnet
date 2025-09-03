// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Resources.Tests;

public sealed class OtelEnvResourceDetectorTests : IDisposable
{
    public OtelEnvResourceDetectorTests()
    {
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void OtelEnvResource_EnvVarKey()
    {
        Assert.Equal("OTEL_RESOURCE_ATTRIBUTES", OtelEnvResourceDetector.EnvVarKey);
    }

    [Fact]
    public void OtelEnvResource_NullEnvVar()
    {
        // Arrange
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.Equal(Resource.Empty, resource);
    }

    [Theory]
    [InlineData("key1=val1,key2=val2", new string[] { "key1", "key2" }, new string[] { "val1", "val2" })]
    [InlineData("key1,key2=val2", new string[] { "key2" }, new string[] { "val2" })]
    [InlineData("key=Am%C3%A9lie", new string[] { "key" }, new string[] { "Amélie" })] // Valid percent-encoded value
    [InlineData("key1=val1,key2=val2==3", new string[] { "key1", "key2" }, new string[] { "val1", "val2==3" })] // Valid value with equal sign
    [InlineData("key1=,key2=val2", new string[] { "key2" }, new string[] { "val2" })] // Empty value for key1
    [InlineData("=val1,key2=val2", new string[] { "key2" }, new string[] { "val2" })] // Empty key for key1
    [InlineData("Amélie=val", new string[] { }, new string[] { })] // Invalid key
    [InlineData("key=invalid%encoding", new string[] { "key" }, new string[] { "invalid%encoding" })] // Invalid value
    [InlineData("key=v1+v2", new string[] { "key" }, new string[] { "v1+v2" })]
    [InlineData("key=a%E0%80Am%C3%A9lie", new string[] { "key" }, new string[] { "a��Amélie" })]
    public void OtelEnvResource_EnvVar_Validation(string envVarValue, string[] expectedKeys, string[] expectedValues)
    {
        // Arrange
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Equal(expectedKeys.Length, expectedValues.Length);
        Assert.Equal(expectedKeys.Length, resource.Attributes.Count());
        for (int i = 0; i < expectedKeys.Length; i++)
        {
            Assert.Equal(
                expectedKeys.Zip(expectedValues, (k, v) => new KeyValuePair<string, object>(k, v)), resource.Attributes);
        }
    }

    [Fact]
    public void OtelEnvResource_UsingIConfiguration()
    {
        var values = new Dictionary<string, string?>()
        {
            [OtelEnvResourceDetector.EnvVarKey] = "Key1=Val1,Key2=Val2",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var resource = new OtelEnvResourceDetector(configuration).Detect();

        Assert.NotEqual(Resource.Empty, resource);
        Assert.Contains(new KeyValuePair<string, object>("Key1", "Val1"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("Key2", "Val2"), resource.Attributes);
    }
}
