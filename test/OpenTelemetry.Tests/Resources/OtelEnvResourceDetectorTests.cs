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

    [Fact]
    public void OtelEnvResource_WithEnvVar_1()
    {
        // Arrange
        var envVarValue = "Key1=Val1,Key2=Val2";
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Contains(new KeyValuePair<string, object>("Key1", "Val1"), resource.Attributes);
    }

    [Fact]
    public void OtelEnvResource_WithEnvVar_2()
    {
        // Arrange
        var envVarValue = "Key1,Key2=Val2";
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Single(resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("Key2", "Val2"), resource.Attributes);
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

    [Fact]
    public void OtelEnvResource_InvalidValueEncoding()
    {
        // Arrange
        var envVarValue = "key=invalid%encoding";
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Single(resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("key", "invalid%encoding"), resource.Attributes);
    }

    [Fact]
    public void OtelEnvResource_InvalidKeyEncoding()
    {
        // Arrange
        var envVarValue = "Amélie=value";
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Empty(resource.Attributes);
    }

    [Fact]
    public void OtelEnvResource_ValidPercentEncodedValue()
    {
        // Arrange
        var envVarValue = "key=Am%C3%A9lie"; // "Amélie" with é encoded
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Single(resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("key", "Amélie"), resource.Attributes);
    }

    [Fact]
    public void OtelEnvResource_ValidValueEncodingWithEqualSign()
    {
        // Arrange
        var envVarValue = "key1=value1,key2=value2==3";
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Contains(new KeyValuePair<string, object>("key1", "value1"), resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("key2", "value2==3"), resource.Attributes);
    }

    [Fact]
    public void OtelEnvResource_EmptyValue()
    {
        // Arrange
        var envVarValue = "key1=,key2=val2";
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Single(resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("key2", "val2"), resource.Attributes);
    }

    [Fact]
    public void OtelEnvResource_EmptyKey()
    {
        // Arrange
        var envVarValue = "=val1,key2=val2";
        Environment.SetEnvironmentVariable(OtelEnvResourceDetector.EnvVarKey, envVarValue);
        var resource = new OtelEnvResourceDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Single(resource.Attributes);
        Assert.Contains(new KeyValuePair<string, object>("key2", "val2"), resource.Attributes);
    }
}
