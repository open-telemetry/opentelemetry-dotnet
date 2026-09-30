// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OpenTelemetry.Logs.Tests;

public sealed class LogRecordLimitOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var options = new LogRecordLimitOptions();

        Assert.Null(options.AttributeValueLengthLimit);
        Assert.Equal(128, options.AttributeCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_LogRecordSpecificKeys()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [LogRecordLimitOptions.AttributeValueLengthLimitEnvVarKey] = "32",
                [LogRecordLimitOptions.AttributeCountLimitEnvVarKey] = "16",
            })
            .Build();
        var general = new AttributeLimitOptions(config);
        var options = new LogRecordLimitOptions(config, general);

        Assert.Equal(32, options.AttributeValueLengthLimit);
        Assert.Equal(16, options.AttributeCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_LogRecordFallsBackToGeneralAttributeLimits()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AttributeLimitOptions.AttributeValueLengthLimitEnvVarKey] = "48",
                [AttributeLimitOptions.AttributeCountLimitEnvVarKey] = "24",
            })
            .Build();
        var general = new AttributeLimitOptions(config);
        var options = new LogRecordLimitOptions(config, general);

        Assert.Equal(48, options.AttributeValueLengthLimit);
        Assert.Equal(24, options.AttributeCountLimit);
    }

    [Fact]
    public void ConfigurationOverride_NegativeValuesFallBackToGeneralAttributeLimits()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [LogRecordLimitOptions.AttributeValueLengthLimitEnvVarKey] = "-1",
                [LogRecordLimitOptions.AttributeCountLimitEnvVarKey] = "-1",
            })
            .Build();
        var general = new AttributeLimitOptions
        {
            AttributeValueLengthLimit = 50,
            AttributeCountLimit = 64,
        };
        var options = new LogRecordLimitOptions(config, general);

        Assert.Equal(50, options.AttributeValueLengthLimit);
        Assert.Equal(64, options.AttributeCountLimit);
    }

    [Fact]
    public void SettersRejectNegativeValues()
    {
        var options = new LogRecordLimitOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.AttributeValueLengthLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.AttributeCountLimit = -1);
    }

    [Fact]
    public void SettersAcceptValidValues()
    {
        var options = new LogRecordLimitOptions
        {
            AttributeValueLengthLimit = 100,
            AttributeCountLimit = 50,
        };

        Assert.Equal(100, options.AttributeValueLengthLimit);
        Assert.Equal(50, options.AttributeCountLimit);
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
                    [LogRecordLimitOptions.AttributeValueLengthLimitEnvVarKey] = "40",
                })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenTelemetrySharedProviderBuilderServices();
        services.AddOpenTelemetryLoggerProviderBuilderServices();
        services.Configure<AttributeLimitOptions>(options =>
        {
            options.AttributeValueLengthLimit = 30;
            options.AttributeCountLimit = 20;
        });
        services.Configure<LogRecordLimitOptions>(options => options.AttributeValueLengthLimit = 10);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptionsMonitor<LogRecordLimitOptions>>().CurrentValue;

        Assert.Equal(10, options.AttributeValueLengthLimit);
        Assert.Equal(20, options.AttributeCountLimit);
    }
}
