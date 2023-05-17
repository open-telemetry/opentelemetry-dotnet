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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
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
        OpenTelemetryLoggerOptions? addOpenTelemetryOptionsInstance = null;
        OpenTelemetryLoggerOptions? configureOptionsInstance = null;

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

        if (numberOfBuilderRegistrations > 0)
        {
            Assert.NotNull(addOpenTelemetryOptionsInstance);
        }
        else
        {
            Assert.Null(addOpenTelemetryOptionsInstance);
        }

        if (numberOfOptionsRegistrations > 0)
        {
            Assert.NotNull(configureOptionsInstance);
        }
        else
        {
            Assert.Null(configureOptionsInstance);
        }

        if (numberOfBuilderRegistrations > 0 || numberOfOptionsRegistrations > 0)
        {
            Assert.False(ReferenceEquals(addOpenTelemetryOptionsInstance, configureOptionsInstance));
        }

        Assert.Equal(numberOfBuilderRegistrations, configureCallbackInvocations);
        Assert.Equal(numberOfOptionsRegistrations, optionsCallbackInvocations);

        void ConfigureCallback(OpenTelemetryLoggerOptions options)
        {
            if (addOpenTelemetryOptionsInstance == null)
            {
                addOpenTelemetryOptionsInstance = options;
            }
            else
            {
                Assert.False(ReferenceEquals(configureOptionsInstance, options));
            }

            configureCallbackInvocations++;
        }

        void OptionsCallback(OpenTelemetryLoggerOptions options)
        {
            if (configureOptionsInstance == null)
            {
                configureOptionsInstance = options;
            }
            else
            {
                Assert.True(ReferenceEquals(configureOptionsInstance, options));
                Assert.Equal(configureOptionsInstance, options);
            }

            optionsCallbackInvocations++;
        }
    }

    [Fact]
    public void ServiceCollectionAddOpenTelemetryConfigureOpenTelemetryTest()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.IncludeFormattedMessage = true;

                options.AddProcessor(new CustomProcessor());
                options.SetResourceBuilder(
                    ResourceBuilder.CreateEmpty().AddAttributes(
                        new Dictionary<string, object> { ["key1"] = "value1" }));

                options.ConfigureOpenTelemetry(builder => builder
                    .ConfigureServices(services => services.AddSingleton<CustomType>())
                    .ConfigureResource(r => r.AddAttributes(new Dictionary<string, object> { ["key2"] = "value2" }))
                    .AddProcessor(new CustomProcessor())
                    .AddProcessor<CustomProcessor>());
            }));

        serviceCollection.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            options.ConfigureOpenTelemetry(builder =>
            {
                Assert.Throws<NotSupportedException>(() => builder.AddProcessor<CustomProcessor>());
                builder.AddProcessor(new CustomProcessor());

                var deferredBuilder = builder as IDeferredLoggerProviderBuilder;
                Assert.NotNull(deferredBuilder);

                deferredBuilder.Configure((sp, builder) =>
                    builder.ConfigureResource(r => r.AddAttributes(new Dictionary<string, object> { ["key3"] = "value3" })));
            });
        });

        using var sp = serviceCollection.BuildServiceProvider();

        var customType = sp.GetService<CustomType>();

        Assert.NotNull(customType);

        var loggerFactory = sp.GetService<ILoggerFactory>();

        Assert.NotNull(loggerFactory);

        var loggerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(loggerProvider);

        var compositeProcessor = loggerProvider.Processor as CompositeProcessor<LogRecord>;

        Assert.NotNull(compositeProcessor);

        var processorCount = 0;
        var current = compositeProcessor.Head;
        while (current != null)
        {
            processorCount++;
            current = current.Next;
        }

        Assert.Equal(4, processorCount);

        Assert.Contains(loggerProvider.Resource.Attributes, kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(loggerProvider.Resource.Attributes, kvp => kvp.Key == "key2" && (string)kvp.Value == "value2");
        Assert.Contains(loggerProvider.Resource.Attributes, kvp => kvp.Key == "key3" && (string)kvp.Value == "value3");

        var iloggerProviders = sp.GetServices<ILoggerProvider>();

        Assert.NotNull(iloggerProviders);
        Assert.Single(iloggerProviders);

        var iloggerProvider = iloggerProviders.First() as OpenTelemetryLoggerProvider;

        Assert.NotNull(iloggerProvider);
    }

    private sealed class CustomType
    {
    }

    private sealed class CustomProcessor : BaseProcessor<LogRecord>
    {
    }
}
