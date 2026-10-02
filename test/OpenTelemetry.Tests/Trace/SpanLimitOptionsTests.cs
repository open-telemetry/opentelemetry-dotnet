// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Trace.Tests;

public sealed class SpanLimitOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var options = new SpanLimitOptions();

        Assert.Null(options.AttributeValueLengthLimit);
        Assert.Equal(128, options.AttributeCountLimit);
        Assert.Equal(128, options.EventCountLimit);
        Assert.Equal(128, options.LinkCountLimit);
        Assert.Equal(128, options.AttributePerEventCountLimit);
        Assert.Equal(128, options.AttributePerLinkCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_SpanAttributeCountLimitDoesNotApplyToEventAndLinkAttributes()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SpanLimitOptions.AttributeCountLimitEnvVarKey] = "10",
            })
            .Build();
        var options = new SpanLimitOptions(config, new AttributeLimitOptions(config));

        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(128, options.AttributePerEventCountLimit);
        Assert.Equal(128, options.AttributePerLinkCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_SpanSpecificKeys()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SpanLimitOptions.AttributeValueLengthLimitEnvVarKey] = "16",
                [SpanLimitOptions.AttributeCountLimitEnvVarKey] = "8",
                [SpanLimitOptions.EventCountLimitEnvVarKey] = "4",
                [SpanLimitOptions.LinkCountLimitEnvVarKey] = "3",
                [SpanLimitOptions.AttributePerEventCountLimitEnvVarKey] = "5",
                [SpanLimitOptions.AttributePerLinkCountLimitEnvVarKey] = "6",
            })
            .Build();
        var general = new AttributeLimitOptions(config);
        var options = new SpanLimitOptions(config, general);

        Assert.Equal(16, options.AttributeValueLengthLimit);
        Assert.Equal(8, options.AttributeCountLimit);
        Assert.Equal(4, options.EventCountLimit);
        Assert.Equal(3, options.LinkCountLimit);
        Assert.Equal(5, options.AttributePerEventCountLimit);
        Assert.Equal(6, options.AttributePerLinkCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_SpanFallsBackToGeneralAttributeLimits()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AttributeLimitOptions.AttributeValueLengthLimitEnvVarKey] = "20",
                [AttributeLimitOptions.AttributeCountLimitEnvVarKey] = "10",
            })
            .Build();
        var general = new AttributeLimitOptions(config);
        var options = new SpanLimitOptions(config, general);

        Assert.Equal(20, options.AttributeValueLengthLimit);
        Assert.Equal(10, options.AttributeCountLimit);
        Assert.Equal(128, options.EventCountLimit);
        Assert.Equal(128, options.LinkCountLimit);
        Assert.Equal(10, options.AttributePerEventCountLimit);
        Assert.Equal(10, options.AttributePerLinkCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_NegativeValuesUseApplicableFallbacks()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [SpanLimitOptions.AttributeValueLengthLimitEnvVarKey] = "-1",
                [SpanLimitOptions.AttributeCountLimitEnvVarKey] = "-1",
                [SpanLimitOptions.EventCountLimitEnvVarKey] = "-1",
                [SpanLimitOptions.LinkCountLimitEnvVarKey] = "-1",
                [SpanLimitOptions.AttributePerEventCountLimitEnvVarKey] = "-1",
                [SpanLimitOptions.AttributePerLinkCountLimitEnvVarKey] = "-1",
            })
            .Build();
        var general = new AttributeLimitOptions
        {
            AttributeValueLengthLimit = 50,
            AttributeCountLimit = 64,
        };
        var options = new SpanLimitOptions(config, general);

        Assert.Equal(50, options.AttributeValueLengthLimit);
        Assert.Equal(64, options.AttributeCountLimit);
        Assert.Equal(128, options.EventCountLimit);
        Assert.Equal(128, options.LinkCountLimit);
        Assert.Equal(64, options.AttributePerEventCountLimit);
        Assert.Equal(64, options.AttributePerLinkCountLimit);
    }

    [Fact]
    public void SettersRejectNegativeValues()
    {
        var options = new SpanLimitOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.AttributeValueLengthLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.AttributeCountLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.EventCountLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.LinkCountLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.AttributePerEventCountLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.AttributePerLinkCountLimit = -1);
    }

    [Fact]
    public void SettersAcceptValidValues()
    {
        var options = new SpanLimitOptions
        {
            AttributeValueLengthLimit = 100,
            AttributeCountLimit = 50,
            EventCountLimit = 25,
            LinkCountLimit = 5,
            AttributePerEventCountLimit = 15,
            AttributePerLinkCountLimit = 12,
        };

        Assert.Equal(100, options.AttributeValueLengthLimit);
        Assert.Equal(50, options.AttributeCountLimit);
        Assert.Equal(25, options.EventCountLimit);
        Assert.Equal(5, options.LinkCountLimit);
        Assert.Equal(15, options.AttributePerEventCountLimit);
        Assert.Equal(12, options.AttributePerLinkCountLimit);
    }

    [Fact]
    public void OptionsFactoryAppliesSignalConfigurationAfterGeneralOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [AttributeLimitOptions.AttributeValueLengthLimitEnvVarKey] = "100",
                    [AttributeLimitOptions.AttributeCountLimitEnvVarKey] = "50",
                    [SpanLimitOptions.AttributeValueLengthLimitEnvVarKey] = "40",
                    [SpanLimitOptions.AttributeCountLimitEnvVarKey] = "30",
                })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.AddOpenTelemetryTracerProviderBuilderServices();
        services.Configure<AttributeLimitOptions>(options =>
        {
            options.AttributeValueLengthLimit = 20;
            options.AttributeCountLimit = 10;
        });
        services.Configure<SpanLimitOptions>(options => options.AttributeValueLengthLimit = 5);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<SpanLimitOptions>>().CurrentValue;

        Assert.Equal(5, options.AttributeValueLengthLimit);
        Assert.Equal(30, options.AttributeCountLimit);
        Assert.Equal(10, options.AttributePerEventCountLimit);
        Assert.Equal(10, options.AttributePerLinkCountLimit);
    }
}
