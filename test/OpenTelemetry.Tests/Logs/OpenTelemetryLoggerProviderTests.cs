// <copyright file="OpenTelemetryLoggerProviderTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

        var provider = new OpenTelemetryLoggerProvider(optionsMonitor);

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
