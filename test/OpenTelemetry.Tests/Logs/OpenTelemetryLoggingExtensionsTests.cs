// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class OpenTelemetryLoggingExtensionsTests
{
    [Fact]
    public void ServiceCollectionAddOpenTelemetryNoParametersTest()
    {
        bool optionsCallbackInvoked = false;

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(configure =>
        {
            configure.AddOpenTelemetry();
        });

        serviceCollection.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            optionsCallbackInvoked = true;
        });

        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        ILoggerFactory? loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        Assert.NotNull(loggerFactory);

        Assert.True(optionsCallbackInvoked);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    public void ServiceCollectionAddOpenTelemetryConfigureActionTests(int numberOfBuilderRegistrations, int numberOfOptionsRegistrations)
    {
        int configureCallbackInvocations = 0;
        int optionsCallbackInvocations = 0;
        OpenTelemetryLoggerOptions? optionsInstance = null;

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(configure =>
        {
            for (int i = 0; i < numberOfBuilderRegistrations; i++)
            {
                configure.AddOpenTelemetry(ConfigureCallback);
            }
        });

        for (int i = 0; i < numberOfOptionsRegistrations; i++)
        {
            serviceCollection.Configure<OpenTelemetryLoggerOptions>(OptionsCallback);
        }

        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        ILoggerFactory? loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        Assert.NotNull(loggerFactory);

        Assert.NotNull(optionsInstance);

        Assert.Equal(numberOfBuilderRegistrations, configureCallbackInvocations);
        Assert.Equal(numberOfOptionsRegistrations, optionsCallbackInvocations);

        void ConfigureCallback(OpenTelemetryLoggerOptions options)
        {
            if (optionsInstance == null)
            {
                optionsInstance = options;
            }
            else
            {
                Assert.Equal(optionsInstance, options);
            }

            configureCallbackInvocations++;
        }

        void OptionsCallback(OpenTelemetryLoggerOptions options)
        {
            if (optionsInstance == null)
            {
                optionsInstance = options;
            }
            else
            {
                Assert.Equal(optionsInstance, options);
            }

            optionsCallbackInvocations++;
        }
    }

    // This test validates that the OpenTelemetryLoggerOptions contains only primitive type properties.
    // This is necessary to ensure trim correctness since that class is effectively deserialized from
    // configuration. The top level properties are ensured via annotation on the RegisterProviderOptions API
    // but if there was any complex type property, members of the complex type would not be preserved
    // and could lead to incompatibilities with trimming.
    [Fact]
    public void TestTrimmingCorrectnessOfOpenTelemetryLoggerOptions()
    {
        foreach (var prop in typeof(OpenTelemetryLoggerOptions).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            Assert.True(prop.PropertyType.IsPrimitive, $"Property OpenTelemetryLoggerOptions.{prop.Name} doesn't have a primitive type. This is potentially a trim compatibility issue.");
        }
    }
}
