// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Extensions.ProcessContext.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.ProcessContext.Tests;

public sealed class ProcessContextProviderTests : IDisposable
{
    private readonly FakeProcessContextPlatform platform = new();
    private readonly ProcessContextRegistry registry;

    public ProcessContextProviderTests()
    {
        this.registry = new ProcessContextRegistry(new ProcessContextWriter(this.platform));
    }

    [Fact]
    public void TracerProviderPublishesItsResolvedResource()
    {
        using var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(r => r.AddService("checkout", serviceInstanceId: "instance-1"))
            .AddInstrumentation(_ => this.Registration(enabled: true))
            .Build();

        var published = this.PublishedAttributes();
        Assert.Equal("checkout", published["service.name"]);
        Assert.Equal("instance-1", published["service.instance.id"]);

        // Exactly the provider's resource, including the SDK defaults.
        Assert.Equal(
            provider.GetResource().Attributes.Select(a => a.Key).OrderBy(k => k, StringComparer.Ordinal),
            published.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("dotnet", published["telemetry.sdk.language"]);
    }

    [Fact]
    public void MeterAndLoggerProvidersPublish()
    {
        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("metrics-svc"))
            .AddInstrumentation(_ => this.Registration(enabled: true))
            .Build())
        {
            Assert.Equal("metrics-svc", this.PublishedAttributes()["service.name"]);
        }

        Assert.Equal(0, this.registry.Writer.MappingAddress);

        using var loggerProvider = Sdk.CreateLoggerProviderBuilder()
            .ConfigureResource(r => r.AddService("logs-svc"))
            .AddInstrumentation(_ => this.Registration(enabled: true))
            .Build();

        Assert.Equal("logs-svc", this.PublishedAttributes()["service.name"]);
    }

    [Fact]
    public void DisabledPublishesNothing()
    {
        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddInstrumentation(_ => this.Registration(enabled: false))
            .Build();

        Assert.Empty(this.platform.Calls);
    }

    [Fact]
    public void ResourceOverrideIsPublished()
    {
        using var provider = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(r => r.AddService("from-provider"))
            .AddInstrumentation(_ => this.Registration(enabled: true, ResourceBuilder.CreateEmpty().AddService("override").Build()))
            .Build();

        Assert.Equal("override", this.PublishedAttributes()["service.name"]);
    }

    [Fact]
    public void ProvidersSharingAResourcePublishOnce()
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("shared", serviceInstanceId: "i"))
            .AddInstrumentation(_ => this.Registration(enabled: true))
            .Build();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService("shared", serviceInstanceId: "i"))
            .AddInstrumentation(_ => this.Registration(enabled: true))
            .Build();

        Assert.Equal(["CreateMapping", "Madvise", "NameMapping"], this.platform.Calls);
    }

    [Fact]
    public void LatestLiveProviderWinsAndContextIsDroppedWithTheLast()
    {
        var first = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(r => r.AddService("first"))
            .AddInstrumentation(_ => this.Registration(enabled: true))
            .Build();
        var second = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource(r => r.AddService("second"))
            .AddInstrumentation(_ => this.Registration(enabled: true))
            .Build();

        Assert.Equal("second", this.PublishedAttributes()["service.name"]);

        second.Dispose();
        Assert.Equal("first", this.PublishedAttributes()["service.name"]);

        first.Dispose();
        Assert.Equal(0, this.registry.Writer.MappingAddress);
        Assert.Empty(this.platform.LiveMappings);
        Assert.Empty(this.platform.LivePayloads);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("not-a-bool", false)]
    [InlineData(null, false)]
    public void EnabledComesFromConfiguration(string? value, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(value == null ? [] : [new(ProcessContextOptions.EnabledEnvVarKey, value)])
            .Build();

        Assert.Equal(expected, new ProcessContextOptions(configuration).Enabled);
    }

    [Fact]
    public void CodeOverridesConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new(ProcessContextOptions.EnabledEnvVarKey, "false")])
            .Build();
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();

        using var fromConfig = ProcessContextBuilderHelper.CreateRegistration(serviceProvider, configure: null);
        using var fromCode = ProcessContextBuilderHelper.CreateRegistration(serviceProvider, o => o.Enabled = true);

        Assert.False(fromConfig.Options.Enabled);
        Assert.True(fromCode.Options.Enabled);
    }

    public void Dispose() => this.platform.Dispose();

    private ProcessContextRegistration Registration(bool enabled, Resource? resource = null)
        => new(new ProcessContextOptions(new ConfigurationBuilder().Build()) { Enabled = enabled, Resource = resource }, this.registry);

    private Dictionary<string, string> PublishedAttributes()
    {
        var header = HeaderReader.Read(this.registry.Writer.MappingAddress);
        Assert.NotEqual(0UL, header.PublishedAt);
        return header.DecodePayload().Resource.Attributes.ToDictionary(kv => kv.Key, kv => kv.Value.StringValue);
    }
}
