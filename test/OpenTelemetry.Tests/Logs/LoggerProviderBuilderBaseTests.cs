// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static OpenTelemetry.OpenTelemetrySdk;

namespace OpenTelemetry.Logs.Tests;

public sealed class LoggerProviderBuilderBaseTests
{
    [Theory]
    [InlineData("true", typeof(NoopLoggerProvider))]
    [InlineData("false", typeof(LoggerProviderSdk))]
    [InlineData(null, typeof(LoggerProviderSdk))]
    public void LoggerProviderIsExpectedType(string? value, Type expected)
    {
        using (EnvironmentVariableScope.Create("OTEL_SDK_DISABLED", value))
        {
            var builder = new LoggerProviderBuilderBase();

            using var provider = builder.Build();

            Assert.IsType(expected, provider);
        }
    }

    [Fact]
    public void ProviderInterfacePropertyReturnsNull()
    {
        var builder = new LoggerProviderBuilderBase();

        Assert.Null(((ILoggerProviderBuilder)builder).Provider);
    }

    [Fact]
    public void ServiceCollectionConstructorReturnsSdkDisabledProviderWhenDisabled()
    {
        using (EnvironmentVariableScope.Create("OTEL_SDK_DISABLED", "true"))
        {
            var services = new ServiceCollection();

            // Simulate what a real host registers before handing services to the SDK.
            services.AddSingleton<IConfiguration>(_ => new ConfigurationBuilder().AddEnvironmentVariables().Build());

            _ = new LoggerProviderBuilderBase(services);

            using var serviceProvider = services.BuildServiceProvider();
            using var provider = serviceProvider.GetRequiredService<LoggerProvider>();

            Assert.IsType<NoopLoggerProvider>(provider);
        }
    }
}
