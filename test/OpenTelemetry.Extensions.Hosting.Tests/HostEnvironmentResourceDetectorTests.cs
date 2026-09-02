// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Extensions.Hosting.Implementation;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class HostEnvironmentResourceDetectorTests
{
    [Fact]
    public void Detect_NullConfiguration_AppliesHostDefaults()
    {
        var env = CreateEnvironment("MyApp", "Staging");
        var detector = new HostEnvironmentResourceDetector(env, configuration: null);

        var resource = detector.Detect();

        Assert.Contains(resource.Attributes, a => a.Key == "service.name"
            && (string)a.Value == "MyApp");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name"
            && (string)a.Value == "Staging");
    }

    [Fact]
    public void IsAttributeSetInConfiguration_NullConfiguration_ReturnsFalse()
    {
        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), configuration: null);

        Assert.False(detector.IsAttributeSetInConfiguration("service.name"));
        Assert.False(detector.IsAttributeSetInConfiguration("deployment.environment.name"));
    }

    [Fact]
    public void IsAttributeInOtelResourceAttributes_NullConfiguration_ReturnsFalse()
    {
        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), configuration: null);

        Assert.False(detector.IsAttributeInOtelResourceAttributes("service.name"));
    }

    [Fact]
    public void IsAttributeSetInConfiguration_OtelServiceNameSet_ReturnsTrueForServiceName()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = "env-service",
        });

        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.True(detector.IsAttributeSetInConfiguration("service.name"));
        Assert.False(detector.IsAttributeSetInConfiguration("deployment.environment.name"));
    }

    [Fact]
    public void IsAttributeSetInConfiguration_OtelServiceNameEmpty_ReturnsFalseForServiceName()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = string.Empty,
        });

        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.False(detector.IsAttributeSetInConfiguration("service.name"));
    }

    [Fact]
    public void IsAttributeSetInConfiguration_OtelServiceNameWhitespace_ReturnsFalseForServiceName()
    {
        var config = BuildConfiguration(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "   " });
        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.False(detector.IsAttributeSetInConfiguration("service.name"));
    }

    [Fact]
    public void IsAttributeInOtelResourceAttributes_AttributePresent_ReturnsTrue()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=from-env",
        });

        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.True(detector.IsAttributeInOtelResourceAttributes("service.name"));
    }

    [Fact]
    public void IsAttributeInOtelResourceAttributes_AttributeAbsent_ReturnsFalse()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "other.key=value",
        });

        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.False(detector.IsAttributeInOtelResourceAttributes("service.name"));
    }

    [Fact]
    public void IsAttributeInOtelResourceAttributes_LeadingEquals_ReturnsFalse()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "=value",
        });

        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.False(detector.IsAttributeInOtelResourceAttributes("service.name"));
    }

    [Fact]
    public void IsAttributeInOtelResourceAttributes_KeyWithWhitespace_ReturnsTrue()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = " service.name = trimmed",
        });

        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.True(detector.IsAttributeInOtelResourceAttributes("service.name"));
    }

    [Fact]
    public void IsAttributeInOtelResourceAttributes_DifferentCase_ReturnsFalse()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "SERVICE.NAME=value",
        });

        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.False(detector.IsAttributeInOtelResourceAttributes("service.name"));
    }

    [Fact]
    public void IsAttributeInOtelResourceAttributes_MalformedEntryBeforeValid_SkipsMalformed()
    {
        var config = BuildConfiguration(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "malformed,service.name=valid",
        });

        var detector = new HostEnvironmentResourceDetector(CreateEnvironment("App", "Prod"), config);

        Assert.True(detector.IsAttributeInOtelResourceAttributes("service.name"));
        Assert.False(detector.IsAttributeInOtelResourceAttributes("malformed"));
    }

    private static FakeHostEnvironment CreateEnvironment(string applicationName, string environmentName)
        => new() { ApplicationName = applicationName, EnvironmentName = environmentName };

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
