// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Resources.Tests;

[Collection(EnvVarsCollectionDefinition.Name)]
public sealed class EnvironmentVariableResourceIntegrationTests
{
    [Fact]
    public void TracerProvider_PopulatesResourceFromEnvironmentVariables()
    {
        // End-to-end smoke for the env-var > IConfiguration > Resource chain used by
        // ResourceBuilderExtensions.AddEnvironmentVariableDetector. Drives real OTEL
        // spec variables through ResourceBuilder.CreateDefault and reads the live
        // Resource off the built TracerProvider. Catches any regression that breaks
        // the pipeline between Environment and the SDK's exported resource.
        using (EnvironmentVariableScope.Create([
            ("OTEL_SERVICE_NAME", "e2e-env-var-service"),
            ("OTEL_RESOURCE_ATTRIBUTES", "deployment.environment=test,region=eu-west")]))
        {
            using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();

            var attributes = tracerProvider.GetResource().Attributes;

            Assert.Contains(new KeyValuePair<string, object>("service.name", "e2e-env-var-service"), attributes);
            Assert.Contains(new KeyValuePair<string, object>("deployment.environment", "test"), attributes);
            Assert.Contains(new KeyValuePair<string, object>("region", "eu-west"), attributes);
        }
    }

    [Theory]
    [InlineData("OTEL_RESOURCE_ATTRIBUTES", "otel_test_key=from_environment", "otel_test_key", "from_environment")]
    [InlineData("OTEL_SERVICE_NAME", "service-from-environment", "service.name", "service-from-environment")]
    public void CreateDefault_FallsBackToEnvironmentVariables_WhenConfigurationDoesNotContainOtelSetting(
        string environmentVariableName,
        string environmentVariableValue,
        string attributeName,
        string expectedAttributeValue)
    {
        using var environmentVariableScope = EnvironmentVariableScope.Create(
            environmentVariableName,
            environmentVariableValue);

        var resource = BuildResource(
            new Dictionary<string, string?>
            {
                ["SomeUnrelatedKey"] = "SomeUnrelatedValue",
            });

        Assert.Contains(
            new KeyValuePair<string, object>(attributeName, expectedAttributeValue),
            resource.Attributes);
    }

    [Theory]
    [InlineData("OTEL_RESOURCE_ATTRIBUTES", "otel_test_key=from_environment", "otel_test_key=from_configuration", "otel_test_key", "from_configuration", "from_environment")]
    [InlineData("OTEL_SERVICE_NAME", "service-from-environment", "service-from-configuration", "service.name", "service-from-configuration", "service-from-environment")]
    public void CreateDefault_PrefersConfigurationOverEnvironmentVariables(
        string environmentVariableName,
        string environmentVariableValue,
        string configurationValue,
        string attributeName,
        string expectedAttributeValue,
        string unexpectedAttributeValue)
    {
        using var environmentVariableScope = EnvironmentVariableScope.Create(
            environmentVariableName,
            environmentVariableValue);

        var resource = BuildResource(
            new Dictionary<string, string?>
            {
                [environmentVariableName] = configurationValue,
            });

        Assert.Contains(
            new KeyValuePair<string, object>(attributeName, expectedAttributeValue),
            resource.Attributes);

        Assert.DoesNotContain(
            new KeyValuePair<string, object>(attributeName, unexpectedAttributeValue),
            resource.Attributes);
    }

    [Fact]
    public void CreateDefault_ResolvesEachOtelSettingFromItsAvailableSource()
    {
        using var environmentVariableScope = EnvironmentVariableScope.Create([
            ("OTEL_RESOURCE_ATTRIBUTES", "otel_mixed_key=from_environment"),
            ("OTEL_SERVICE_NAME", "service-from-environment")]);

        var resource = BuildResource(
            new Dictionary<string, string?>
            {
                ["OTEL_SERVICE_NAME"] = "service-from-configuration",
            });

        Assert.Contains(
            new KeyValuePair<string, object>("otel_mixed_key", "from_environment"),
            resource.Attributes);

        Assert.Contains(
            new KeyValuePair<string, object>("service.name", "service-from-configuration"),
            resource.Attributes);

        Assert.DoesNotContain(
            new KeyValuePair<string, object>("service.name", "service-from-environment"),
            resource.Attributes);
    }

    private static Resource BuildResource(IEnumerable<KeyValuePair<string, string?>> configurationValues)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
            .SetResourceBuilder(ResourceBuilder.CreateDefault())
            .Build();

        return tracerProvider.GetResource();
    }
}
