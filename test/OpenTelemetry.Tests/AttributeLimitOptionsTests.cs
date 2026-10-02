// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Tests;

public sealed class AttributeLimitOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var options = new AttributeLimitOptions();

        Assert.Null(options.AttributeValueLengthLimit);
        Assert.Equal(128, options.AttributeCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_AttributeValueLengthLimit()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AttributeLimitOptions.AttributeValueLengthLimitEnvVarKey] = "64",
            })
            .Build();
        var options = new AttributeLimitOptions(config);

        Assert.Equal(64, options.AttributeValueLengthLimit);
        Assert.Equal(128, options.AttributeCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_AttributeCountLimit()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AttributeLimitOptions.AttributeCountLimitEnvVarKey] = "32",
            })
            .Build();
        var options = new AttributeLimitOptions(config);

        Assert.Null(options.AttributeValueLengthLimit);
        Assert.Equal(32, options.AttributeCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_NegativeValuesUseDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AttributeLimitOptions.AttributeValueLengthLimitEnvVarKey] = "-1",
                [AttributeLimitOptions.AttributeCountLimitEnvVarKey] = "-1",
            })
            .Build();
        var options = new AttributeLimitOptions(config);

        Assert.Null(options.AttributeValueLengthLimit);
        Assert.Equal(128, options.AttributeCountLimit);
    }

    [Fact]
    public void SettersRejectNegativeValues()
    {
        var options = new AttributeLimitOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.AttributeValueLengthLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.AttributeCountLimit = -1);
    }

    [Fact]
    public void SettersAcceptValidValues()
    {
        var options = new AttributeLimitOptions
        {
            AttributeValueLengthLimit = 100,
            AttributeCountLimit = 50,
        };

        Assert.Equal(100, options.AttributeValueLengthLimit);
        Assert.Equal(50, options.AttributeCountLimit);
    }

    [Fact]
    public void OptionsFactoryAppliesConfigureAndPostConfigureAfterConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [AttributeLimitOptions.AttributeValueLengthLimitEnvVarKey] = "100",
                    [AttributeLimitOptions.AttributeCountLimitEnvVarKey] = "50",
                })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.Configure<AttributeLimitOptions>(options =>
        {
            options.AttributeValueLengthLimit = 40;
            options.AttributeCountLimit = 30;
        });
        services.PostConfigure<AttributeLimitOptions>(options =>
        {
            options.AttributeValueLengthLimit = 20;
            options.AttributeCountLimit = 10;
        });

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<AttributeLimitOptions>>().CurrentValue;

        Assert.Equal(20, options.AttributeValueLengthLimit);
        Assert.Equal(10, options.AttributeCountLimit);
    }
}
