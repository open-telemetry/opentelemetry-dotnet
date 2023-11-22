// <copyright file="OpenTelemetryLoggingExtensionsTests.cs" company="OpenTelemetry Authors">
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

#nullable enable

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class OpenTelemetryLoggingExtensionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServiceCollectionAddOpenTelemetryNoParametersTest(bool callUseExtension)
    {
        bool optionsCallbackInvoked = false;

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(logging =>
        {
            if (callUseExtension)
            {
                logging.UseOpenTelemetry();
            }
            else
            {
                logging.AddOpenTelemetry();
            }
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
    [InlineData(false, 1, 0)]
    [InlineData(false, 1, 1)]
    [InlineData(false, 5, 5)]
    [InlineData(true, 1, 0)]
    [InlineData(true, 1, 1)]
    [InlineData(true, 5, 5)]
    public void ServiceCollectionAddOpenTelemetryConfigureActionTests(
        bool callUseExtension,
        int numberOfBuilderRegistrations,
        int numberOfOptionsRegistrations)
    {
        int configureCallbackInvocations = 0;
        int optionsCallbackInvocations = 0;
        OpenTelemetryLoggerOptions? optionsInstance = null;

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(logging =>
        {
            for (int i = 0; i < numberOfBuilderRegistrations; i++)
            {
                if (callUseExtension)
                {
                    logging.UseOpenTelemetry(configureBuilder: null, configureOptions: ConfigureCallback);
                }
                else
                {
                    logging.AddOpenTelemetry(ConfigureCallback);
                }
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

    [Fact]
    public void UseOpenTelemetryDependencyInjectionTest()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(logging =>
        {
            logging.UseOpenTelemetry(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<TestLogProcessor>();
                });

                builder.ConfigureBuilder((sp, builder) =>
                {
                    builder.AddProcessor(
                        sp.GetRequiredService<TestLogProcessor>());
                });
            });
        });

        using var sp = serviceCollection.BuildServiceProvider();

        var loggerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(loggerProvider);

        Assert.NotNull(loggerProvider.Processor);

        Assert.True(loggerProvider.Processor is TestLogProcessor);
    }

    [Fact]
    public void UseOpenTelemetryOptionsOrderingTest()
    {
        int currentIndex = -1;
        int beforeDelegateIndex = -1;
        int extensionDelegateIndex = -1;
        int afterDelegateIndex = -1;

        var serviceCollection = new ServiceCollection();

        serviceCollection.Configure<OpenTelemetryLoggerOptions>(o => beforeDelegateIndex = ++currentIndex);

        serviceCollection.AddLogging(logging =>
        {
            logging.UseOpenTelemetry(configureBuilder: null, configureOptions: o => extensionDelegateIndex = ++currentIndex);
        });

        serviceCollection.Configure<OpenTelemetryLoggerOptions>(o => afterDelegateIndex = ++currentIndex);

        using var sp = serviceCollection.BuildServiceProvider();

        var loggerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(loggerProvider);

        Assert.Equal(0, beforeDelegateIndex);
        Assert.Equal(1, extensionDelegateIndex);
        Assert.Equal(2, afterDelegateIndex);
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

    [Fact]
    public void VerifyAddProcessorOverloadWithImplementationFactory()
    {
        // arrange
        var services = new ServiceCollection();

        services.AddSingleton<TestLogProcessor>();

        services.AddLogging(logging =>
            logging.AddOpenTelemetry(
                o => o.AddProcessor(sp => sp.GetRequiredService<TestLogProcessor>())));

        // act
        using var sp = services.BuildServiceProvider();

        var loggerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        // assert
        Assert.NotNull(loggerProvider);
        Assert.NotNull(loggerProvider.Processor);
        Assert.True(loggerProvider.Processor is TestLogProcessor);
    }

    [Fact]
    public void VerifyExceptionIsThrownWhenImplementationFactoryIsNull()
    {
        // arrange
        var services = new ServiceCollection();

        services.AddLogging(logging =>
            logging.AddOpenTelemetry(
                o => o.AddProcessor(implementationFactory: null!)));

        // act
        using var sp = services.BuildServiceProvider();

        // assert
        Assert.Throws<ArgumentNullException>(() => sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk);
    }

    private class TestLogProcessor : BaseProcessor<LogRecord>
    {
    }
}
