// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
        Assert.Equal(defaults.Version, provider.Options.Version);
        Assert.Equal(defaults.SchemaUrl, provider.Options.SchemaUrl);

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

    [Fact]
    public void VerifyVersionAndSchemaUrlCannotBeChangedAfterInit()
    {
        var services = new ServiceCollection();

        services.AddOptions<OpenTelemetryLoggerOptions>().Configure(o =>
        {
            o.Version = "1.0.0";
            o.SchemaUrl = "https://opentelemetry.io/schemas/1.0.0";
        });

        using var sp = services.BuildServiceProvider();

        var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>();

        using var provider = new OpenTelemetryLoggerProvider(optionsMonitor);

        // Verify initial set
        Assert.Equal("1.0.0", provider.Options.Version);
        Assert.Equal("https://opentelemetry.io/schemas/1.0.0", provider.Options.SchemaUrl);

        var options = optionsMonitor.CurrentValue;

        Assert.NotNull(options);

        // Attempt to change value
        options.Version = "2.0.0";
        options.SchemaUrl = "https://opentelemetry.io/schemas/2.0.0";

        // Verify options are unchanged
        Assert.Equal("1.0.0", provider.Options.Version);
        Assert.Equal("https://opentelemetry.io/schemas/1.0.0", provider.Options.SchemaUrl);
    }
}
