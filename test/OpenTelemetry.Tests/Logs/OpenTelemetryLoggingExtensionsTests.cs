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

using System;
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

        Assert.NotNull(optionsInstance);

        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        optionsInstance = null;

        ILoggerFactory? loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        Assert.NotNull(loggerFactory);

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
    public void ServiceCollectionAddOpenTelemetryWithProviderTest()
    {
        var provider = new WrappedOpenTelemetryLoggerProvider();

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(provider);
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
    public void ServiceCollectionAddOpenTelemetryWithProviderAndDisposeSpecifiedTests(bool dispose)
    {
        var provider = new WrappedOpenTelemetryLoggerProvider();

        var serviceCollection = new ServiceCollection();

        serviceCollection.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(provider, disposeProvider: dispose);
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
        var provider = new WrappedOpenTelemetryLoggerProvider();

        using (var factory = LoggerFactory.Create(configure =>
        {
            configure.AddOpenTelemetry(provider, disposeProvider: dispose);
        }))
        {
            Assert.False(provider.Disposed);
        }

        Assert.Equal(dispose, provider.Disposed);

        provider.Dispose();

        Assert.True(provider.Disposed);
    }

    [Fact]
    public void ServiceCollectionAddOpenTelemetryServicesAvailableTest()
    {
        int invocationCount = 0;

        var services = new ServiceCollection();

        services.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(options =>
            {
                invocationCount++;
                Assert.NotNull(options.Services);
            });
        });

        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            invocationCount++;

            // Note: Services are no longer available once OpenTelemetryLoggerOptions has been created

            Assert.Null(options.Services);
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        Assert.Equal(2, invocationCount);
    }

    [Fact]
    public void ServiceCollectionAddOpenTelemetryProcessorThroughDependencyWithoutRegistrationThrowsTest()
    {
        var services = new ServiceCollection();

        services.AddLogging(configure =>
        {
            // Note: This will throw because CustomProcessor has not been
            // registered with services

            configure.AddOpenTelemetry(options => options.AddProcessor<CustomProcessor>());
        });

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => serviceProvider.GetRequiredService<ILoggerFactory>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServiceCollectionAddOpenTelemetryProcessorThroughDependencyWithRegistrationTests(bool registerOutside)
    {
        CustomProcessor.InstanceCount = 0;

        var services = new ServiceCollection();

        if (registerOutside)
        {
            services.AddSingleton<CustomProcessor>();
        }

        services.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(options =>
            {
                if (!registerOutside)
                {
                    options.Services!.AddSingleton<CustomProcessor>();
                }

                options.AddProcessor<CustomProcessor>();
            });
        });

        CustomProcessor? customProcessor = null;

        using (var serviceProvider = services.BuildServiceProvider())
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            customProcessor = serviceProvider.GetRequiredService<CustomProcessor>();

            loggerFactory.Dispose();

            Assert.False(customProcessor.Disposed);
        }

        Assert.True(customProcessor.Disposed);

        Assert.Equal(1, CustomProcessor.InstanceCount);
    }

    [Fact]
    public void ServiceCollectionAddOpenTelemetryConfigureCallbackTest()
    {
        var services = new ServiceCollection();

        services.AddSingleton<TestClass>();

        CustomProcessor? customProcessor = null;

        services.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(options =>
            {
                options.Configure((sp, provider) =>
                {
                    var testClass = sp.GetRequiredService<TestClass>();

                    customProcessor = new CustomProcessor
                    {
                        TestClass = testClass,
                    };

                    provider.AddProcessor(customProcessor);
                });
            });
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        Assert.NotNull(customProcessor?.TestClass);
    }

    [Fact]
    public void ServiceCollectionAddOpenTelemetryExternalRegistrationTest()
    {
        CustomProcessor.InstanceCount = 0;

        var services = new ServiceCollection();

        services.AddSingleton<BaseProcessor<LogRecord>>(sp => new CustomProcessor());
        services.AddSingleton<BaseProcessor<LogRecord>>(sp => new CustomProcessor());

        services.AddLogging(configure =>
        {
            configure.AddOpenTelemetry();
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        Assert.Equal(2, CustomProcessor.InstanceCount);
    }

    private sealed class WrappedOpenTelemetryLoggerProvider : OpenTelemetryLoggerProvider
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
        public CustomProcessor()
        {
            InstanceCount++;
        }

        public static int InstanceCount { get; set; }

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
