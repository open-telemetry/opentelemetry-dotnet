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

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class OpenTelemetryLoggingExtensionsTests
{
    [Fact]
    public void LoggingBuilderAddOpenTelemetryNoParametersTest()
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
    [InlineData(1)]
    [InlineData(5)]
    public void LoggingBuilderAddOpenTelemetryConfigureActionTests(int numberOfOptionsRegistrations)
    {
        int configureCallbackInvocations = 0;
        int optionsCallbackInvocations = 0;
        OpenTelemetryLoggerOptions? optionsInstance = null;

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(); // <- Just to verify this doesn't cause a throw.
            configure.AddOpenTelemetry(ConfigureCallback);
        });

        for (int i = 0; i < numberOfOptionsRegistrations; i++)
        {
            serviceCollection.Configure<OpenTelemetryLoggerOptions>(OptionsCallback);
        }

        Assert.Null(optionsInstance);

        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        ILoggerFactory? loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        Assert.NotNull(loggerFactory);

        Assert.Equal(1, configureCallbackInvocations);
        Assert.Equal(numberOfOptionsRegistrations, optionsCallbackInvocations);

        void ConfigureCallback(OpenTelemetryLoggerOptions options)
        {
            if (optionsInstance == null)
            {
                optionsInstance = options;
            }
            else
            {
                // Note: In the callback phase each options instance is unique
                Assert.NotEqual(optionsInstance, options);
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
                // Note: In the options phase each instance is the same
                Assert.Equal(optionsInstance, options);
            }

            optionsCallbackInvocations++;
        }
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryMultipleBuildersTest()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(configure =>
        {
            configure.AddOpenTelemetry().ConfigureResource(
                r => r.AddAttributes(new Dictionary<string, object>() { ["key1"] = "value1" }));
            configure.AddOpenTelemetry().ConfigureResource(
                r => r.AddAttributes(new Dictionary<string, object>() { ["key2"] = "value2" }));
        });

        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        Assert.NotNull(loggerFactory);

        var provider = serviceProvider.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(provider);

        Assert.Contains(provider!.Resource.Attributes, kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(provider!.Resource.Attributes, kvp => kvp.Key == "key2" && (string)kvp.Value == "value2");
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryWithProviderTest()
    {
        var provider = new WrappedLoggerProvider();

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(provider, configureOptions: null, disposeProvider: true);
        });

        using (ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider())
        {
            ILoggerFactory? loggerFactory = serviceProvider.GetService<ILoggerFactory>();

            Assert.NotNull(loggerFactory);

            loggerFactory!.Dispose();

            // Note: Provider disposal does not actually happen until serviceProvider is disposed
            Assert.False(provider.Disposed);
        }

        Assert.True(provider.Disposed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoggingBuilderAddOpenTelemetryWithProviderAndDisposeSpecifiedTests(bool dispose)
    {
        var provider = new WrappedLoggerProvider();

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(provider, configureOptions: null, disposeProvider: dispose);
        });

        using (ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider())
        {
            ILoggerFactory? loggerFactory = serviceProvider.GetService<ILoggerFactory>();

            Assert.NotNull(loggerFactory);

            loggerFactory!.Dispose();

            // Note: Provider disposal does not actually happen until serviceProvider is disposed
            Assert.False(provider.Disposed);
        }

        Assert.Equal(dispose, provider.Disposed);

        provider.Dispose();

        Assert.True(provider.Disposed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoggerFactoryCreateAddOpenTelemetryWithProviderAndDisposeSpecifiedTests(bool dispose)
    {
        var provider = new WrappedLoggerProvider();

        using (var factory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(provider, configureOptions: null, disposeProvider: dispose);
        }))
        {
            Assert.False(provider.Disposed);
        }

        Assert.Equal(dispose, provider.Disposed);

        provider.Dispose();

        Assert.True(provider.Disposed);
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryProcessorThroughDependencyTest()
    {
        CustomProcessor.InstanceCount = 0;

        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddOpenTelemetry().AddProcessor<CustomProcessor>();
        });

        CustomProcessor? customProcessor = null;

        using (var serviceProvider = services.BuildServiceProvider())
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            customProcessor = serviceProvider.GetRequiredService<CustomProcessor>();

            Assert.NotNull(customProcessor);

            loggerFactory.Dispose();

            Assert.False(customProcessor!.Disposed);
        }

        Assert.True(customProcessor.Disposed);

        Assert.Equal(1, CustomProcessor.InstanceCount);
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryConfigureCallbackTest()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TestClass>();

        CustomProcessor? customProcessor = null;

        services.AddLogging(builder =>
        {
            builder.AddOpenTelemetry().ConfigureBuilder((sp, builder) =>
            {
                var testClass = sp.GetRequiredService<TestClass>();

                customProcessor = new CustomProcessor
                {
                    TestClass = testClass,
                };

                builder.AddProcessor(customProcessor);
            });
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        Assert.NotNull(customProcessor?.TestClass);
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryOptionsOrderingTest()
    {
        var services = new ServiceCollection();

        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            // Note: This will be applied first to the final options
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;
        });

        services.AddLogging(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                // Note: This will be applied second to the final options
                options.IncludeFormattedMessage = false;
                options.ParseStateValues = false;
            });
        });

        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            // Note: This will be applied last to the final options
            options.ParseStateValues = true;
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        var finalOptions = serviceProvider.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

        Assert.False(finalOptions.IncludeFormattedMessage);
        Assert.True(finalOptions.IncludeScopes);
        Assert.True(finalOptions.ParseStateValues);
    }

    [Fact]
    public void LoggingBuilderConfigureOpenTelemetryOrderingTest()
    {
        int configureInvocationCount = 0;

        var services = new ServiceCollection();

        LoggerProviderBuilder? builder = null;

        services.ConfigureOpenTelemetryLogging(options =>
        {
            // Note: This will be applied first to the final options
            options.AddProcessor(new CustomProcessor(0));

            options.ConfigureBuilder((sp, b) =>
            {
                Assert.Null(builder);
                builder = b;
                configureInvocationCount++;
            });
        });

        services.AddLogging(loggingBuilder =>
        {
            var loggerBuilder = loggingBuilder.AddOpenTelemetry();

            // Note: This be applied second to the final options

            loggerBuilder.AddProcessor(new CustomProcessor(1));

            loggerBuilder.ConfigureBuilder((sp, b) =>
            {
                configureInvocationCount++;

                Assert.NotNull(builder);
                Assert.Equal(builder, b);
            });
        });

        services.ConfigureOpenTelemetryLogging(options =>
        {
            options.AddProcessor(new CustomProcessor(2));

            options.ConfigureBuilder((sp, b) =>
            {
                configureInvocationCount++;

                Assert.NotNull(builder);
                Assert.Equal(builder, b);
            });
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        Assert.NotNull(builder);
        Assert.Equal(3, configureInvocationCount);

        var provider = serviceProvider.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(provider);

        var processor = provider!.Processor as CompositeProcessor<LogRecord>;

        Assert.NotNull(processor);

        int count = 0;
        var current = processor!.Head;
        while (current != null)
        {
            var instance = current.Value as CustomProcessor;
            Assert.Equal(count, instance?.Id);

            count++;
            current = current.Next;
        }

        Assert.Equal(3, count);
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryResourceTest()
    {
        var services = new ServiceCollection();

        bool serviceProviderTestExecuted = false;

        services.AddLogging(builder =>
        {
            builder.AddOpenTelemetry().SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Examples.LoggingExtensions"));
        });

        services.ConfigureOpenTelemetryLogging(options =>
        {
            options.ConfigureResource(builder => builder.AddAttributes(new Dictionary<string, object> { ["key1"] = "value1" }));
        });

        services.ConfigureOpenTelemetryLogging(options =>
        {
            options.ConfigureResource(builder =>
            {
                builder.AddDetector(sp =>
                {
                    serviceProviderTestExecuted = true;
                    Assert.NotNull(sp);
                    return new ResourceBuilder.WrapperResourceDetector(new Resource(new Dictionary<string, object>() { ["key2"] = "value2" }));
                });
            });
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        var provider = serviceProvider.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(provider);
        Assert.True(serviceProviderTestExecuted);

        var resource = provider!.Resource;

        Assert.NotNull(resource);

        Assert.Contains(resource.Attributes, kvp => kvp.Key == "service.name");
        Assert.Contains(resource.Attributes, kvp => kvp.Key == "service.instance.id");
        Assert.Contains(resource.Attributes, kvp => kvp.Key == "key1");
        Assert.Contains(resource.Attributes, kvp => kvp.Key == "key2");
    }

    private sealed class WrappedLoggerProvider : LoggerProvider
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            this.Disposed = true;

            base.Dispose(disposing);
        }
    }

    private sealed class CustomProcessor : BaseProcessor<LogRecord>
    {
        public CustomProcessor(int? id = null)
        {
            this.Id = id;
            InstanceCount++;
        }

        public static int InstanceCount { get; set; }

        public int? Id { get; }

        public bool Disposed { get; private set; }

        public TestClass? TestClass { get; set; }

        protected override void Dispose(bool disposing)
        {
            this.Disposed = true;

            base.Dispose(disposing);
        }
    }

    private sealed class TestClass
    {
    }
}
