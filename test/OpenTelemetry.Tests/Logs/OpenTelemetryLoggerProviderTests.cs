// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class OpenTelemetryLoggerProviderTests
{
    [Fact]
    public void DefaultCtorTests()
    {
        var services = new ServiceCollection();
        services.AddOptions();

        using var sp = services.BuildServiceProvider();

        OpenTelemetryLoggerOptions defaults = new();

        using OpenTelemetryLoggerProvider provider = new(sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>());

        Assert.Equal(defaults.IncludeScopes, provider.Options.IncludeScopes);
        Assert.Equal(defaults.IncludeFormattedMessage, provider.Options.IncludeFormattedMessage);
        Assert.Equal(defaults.ParseStateValues, provider.Options.ParseStateValues);

        var providerSdk = provider.Provider as LoggerProviderSdk;

        Assert.NotNull(providerSdk);
        Assert.Null(providerSdk.Processor);
        Assert.NotNull(providerSdk.Resource);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VerifyOptionsCannotBeChangedAfterInit(bool initialValue)
    {
        var services = new ServiceCollection();

        services.AddOptions<OpenTelemetryLoggerOptions>().Configure(o =>
        {
            o.IncludeFormattedMessage = initialValue;
            o.IncludeScopes = initialValue;
            o.ParseStateValues = initialValue;
        });

        using var sp = services.BuildServiceProvider();

        var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>();

        using var provider = new OpenTelemetryLoggerProvider(optionsMonitor);

        // Verify initial set
        Assert.Equal(initialValue, provider.Options.IncludeFormattedMessage);
        Assert.Equal(initialValue, provider.Options.IncludeScopes);
        Assert.Equal(initialValue, provider.Options.ParseStateValues);

        var options = optionsMonitor.CurrentValue;

        Assert.NotNull(options);

        // Attempt to change value
        options.IncludeFormattedMessage = !initialValue;
        options.IncludeScopes = !initialValue;
        options.ParseStateValues = !initialValue;

        // Verify processor is unchanged
        Assert.Equal(initialValue, provider.Options.IncludeFormattedMessage);
        Assert.Equal(initialValue, provider.Options.IncludeScopes);
        Assert.Equal(initialValue, provider.Options.ParseStateValues);
    }
}
