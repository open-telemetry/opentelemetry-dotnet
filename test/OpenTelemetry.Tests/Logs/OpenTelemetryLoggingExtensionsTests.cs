// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Tests;
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

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:OpenTelemetry:IncludeFormattedMessage"] = "true" })
            .Build();

        serviceCollection.Configure<OpenTelemetryLoggerOptions>(o =>
        {
            // Verify this fires BEFORE options are bound
            Assert.False(o.IncludeFormattedMessage);

            beforeDelegateIndex = ++currentIndex;
        });

        serviceCollection.AddLogging(logging =>
        {
            // Note: Typically the host binds logging configuration to the
            // "Logging" section but since we aren't using a host we do this
            // manually.
            logging.AddConfiguration(config.GetSection("Logging"));

            logging.UseOpenTelemetry(
                configureBuilder: null,
                configureOptions: o =>
                {
                    // Verify this fires AFTER options are bound
                    Assert.True(o.IncludeFormattedMessage);

                    extensionDelegateIndex = ++currentIndex;
                });
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CircularReferenceTest(bool requestLoggerProviderDirectly)
    {
        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddOpenTelemetry());

        services.ConfigureOpenTelemetryLoggerProvider(builder => builder.AddProcessor<TestLogProcessorWithILoggerFactoryDependency>());

        using var sp = services.BuildServiceProvider();

        if (requestLoggerProviderDirectly)
        {
            var provider = sp.GetRequiredService<LoggerProvider>();
            Assert.NotNull(provider);
        }
        else
        {
            var factory = sp.GetRequiredService<ILoggerFactory>();
            Assert.NotNull(factory);
        }

        var loggerProvider = sp.GetRequiredService<LoggerProvider>() as LoggerProviderSdk;

        Assert.NotNull(loggerProvider);

        Assert.True(loggerProvider.Processor is TestLogProcessorWithILoggerFactoryDependency);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void OptionReloadingTest(bool useOptionsMonitor, bool useOptionsSnapshot)
    {
        var delegateInvocationCount = 0;

        var root = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(root);

        services.AddLogging(logging => logging
            .AddConfiguration(root.GetSection("logging"))
            .AddOpenTelemetry(options =>
            {
                delegateInvocationCount++;

                options.AddProcessor(new TestLogProcessor());
            }));

        using var sp = services.BuildServiceProvider();

        if (useOptionsMonitor)
        {
            var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>();

            Assert.NotNull(optionsMonitor.CurrentValue);
        }

        if (useOptionsSnapshot)
        {
            using var scope = sp.CreateScope();

            var optionsSnapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<OpenTelemetryLoggerOptions>>();

            Assert.NotNull(optionsSnapshot.Value);
        }

        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        Assert.Equal(1, delegateInvocationCount);

        root.Reload();

        Assert.Equal(1, delegateInvocationCount);
    }

    [Fact]
    public void MixedOptionsUsageTest()
    {
        var root = new ConfigurationBuilder().Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(root);

        services.AddLogging(logging => logging
            .AddConfiguration(root.GetSection("logging"))
            .AddOpenTelemetry(options =>
            {
                options.AddProcessor(new TestLogProcessor());
            }));

        using var sp = services.BuildServiceProvider();

        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

        var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;
        var options = sp.GetRequiredService<IOptions<OpenTelemetryLoggerOptions>>().Value;

        Assert.True(ReferenceEquals(options, optionsMonitor));

        using var scope = sp.CreateScope();

        var optionsSnapshot = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<OpenTelemetryLoggerOptions>>().Value;
        Assert.True(ReferenceEquals(options, optionsSnapshot));
    }

    private sealed class TestLogProcessor : BaseProcessor<LogRecord>
    {
        public bool Disposed;

        protected override void Dispose(bool disposing)
        {
            this.Disposed = true;

            base.Dispose(disposing);
        }
    }

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    private sealed class TestLogProcessorWithILoggerFactoryDependency : BaseProcessor<LogRecord>
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
    {
        private readonly ILogger logger;

        public TestLogProcessorWithILoggerFactoryDependency(ILoggerFactory loggerFactory)
        {
            // Note: It is NOT recommended to log from inside a processor. This
            // test is meant to mirror someone injecting IHttpClientFactory
            // (which itself uses ILoggerFactory) as part of an exporter. That
            // is a more realistic scenario but needs a dependency to do that so
            // here we approximate the graph.
            this.logger = loggerFactory.CreateLogger("MyLogger");
        }

        protected override void Dispose(bool disposing)
        {
            this.logger.DisposedCalled();

            base.Dispose(disposing);
        }
    }
}
