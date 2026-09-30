// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class ResourceBuilderEnvironmentVariableTests
{
    [Theory]
    [InlineData(
        "OTEL_RESOURCE_ATTRIBUTES",
        "otel_test_key=from_environment",
        "otel_test_key",
        "from_environment")]
    [InlineData(
        "OTEL_SERVICE_NAME",
        "service-from-environment",
        "service.name",
        "service-from-environment")]
    public void CreateDefaultFallsBackToEnvironmentVariablesWhenHostConfigurationDoesNotContainOtelSetting(
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
            new KeyValuePair<string, object>(
                attributeName,
                expectedAttributeValue),
            resource.Attributes);
    }

    [Theory]
    [InlineData(
        "OTEL_RESOURCE_ATTRIBUTES",
        "otel_test_key=from_environment",
        "otel_test_key=from_configuration",
        "otel_test_key",
        "from_configuration",
        "from_environment")]
    [InlineData(
        "OTEL_SERVICE_NAME",
        "service-from-environment",
        "service-from-configuration",
        "service.name",
        "service-from-configuration",
        "service-from-environment")]
    public void CreateDefaultPrefersHostConfigurationOverEnvironmentVariables(
        string environmentVariableName,
        string environmentVariableValue,
        string hostConfigurationValue,
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
                [environmentVariableName] = hostConfigurationValue,
            });

        Assert.Contains(
            new KeyValuePair<string, object>(
                attributeName,
                expectedAttributeValue),
            resource.Attributes);

        Assert.DoesNotContain(
            new KeyValuePair<string, object>(
                attributeName,
                unexpectedAttributeValue),
            resource.Attributes);
    }

    [Fact]
    public void CreateDefaultResolvesEachOtelSettingFromItsAvailableSource()
    {
        using var resourceAttributesScope = EnvironmentVariableScope.Create(
            "OTEL_RESOURCE_ATTRIBUTES",
            "otel_mixed_key=from_environment");

        using var serviceNameScope = EnvironmentVariableScope.Create(
            "OTEL_SERVICE_NAME",
            "service-from-environment");

        var resource = BuildResource(
            new Dictionary<string, string?>
            {
                ["OTEL_SERVICE_NAME"] = "service-from-configuration",
            });

        Assert.Contains(
            new KeyValuePair<string, object>(
                "otel_mixed_key",
                "from_environment"),
            resource.Attributes);

        Assert.Contains(
            new KeyValuePair<string, object>(
                "service.name",
                "service-from-configuration"),
            resource.Attributes);

        Assert.DoesNotContain(
            new KeyValuePair<string, object>(
                "service.name",
                "service-from-environment"),
            resource.Attributes);
    }

    private static Resource BuildResource(
        IEnumerable<KeyValuePair<string, string?>> configurationValues)
    {
        using var host = new HostBuilder()
            .ConfigureAppConfiguration(builder =>
            {
                builder.AddInMemoryCollection(configurationValues);
            })
            .ConfigureServices(services =>
            {
                services
                    .AddOpenTelemetry()
                    .WithMetrics(builder =>
                        builder.SetResourceBuilder(ResourceBuilder.CreateDefault()));
            })
            .Build();

        var meterProvider = host.Services.GetRequiredService<MeterProvider>();
        return ((MeterProviderSdk)meterProvider).Resource;
    }
}
