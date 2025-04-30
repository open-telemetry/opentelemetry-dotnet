// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Xunit;

namespace OpenTelemetry.Resources.Tests;

public sealed class OtelServiceNameEnvVarDetectorTests : IDisposable
{
    public OtelServiceNameEnvVarDetectorTests()
    {
        Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void OtelServiceNameEnvVar_EnvVarKey()
    {
        Assert.Equal("OTEL_SERVICE_NAME", OtelServiceNameEnvVarDetector.EnvVarKey);
    }

    [Fact]
    public void OtelServiceNameEnvVar_Null()
    {
        // Act
        var resource = new OtelServiceNameEnvVarDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.Equal(Resource.Empty, resource);
    }

    [Fact]
    public void OtelServiceNameEnvVar_WithValue()
    {
        // Arrange
        var envVarValue = "my-service";
        Environment.SetEnvironmentVariable(OtelServiceNameEnvVarDetector.EnvVarKey, envVarValue);

        // Act
        var resource = new OtelServiceNameEnvVarDetector(
            new ConfigurationBuilder().AddEnvironmentVariables().Build())
            .Detect();

        // Assert
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, envVarValue), resource.Attributes);
    }

    [Fact]
    public void OtelServiceNameEnvVar_UsingIConfiguration()
    {
        var values = new Dictionary<string, string?>()
        {
            [OtelServiceNameEnvVarDetector.EnvVarKey] = "my-service",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var resource = new OtelServiceNameEnvVarDetector(configuration).Detect();

        Assert.NotEqual(Resource.Empty, resource);
        Assert.Contains(new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "my-service"), resource.Attributes);
    }
}
