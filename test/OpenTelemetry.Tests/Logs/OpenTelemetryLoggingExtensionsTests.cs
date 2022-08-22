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

        Assert.NotNull(optionsInstance);

        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        optionsInstance = null;

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

        OpenTelemetryLoggerProvider? provider = null;

        serviceCollection.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(options
                => options.ConfigureResource(
                    r => r.AddAttributes(new Dictionary<string, object>() { ["key1"] = "value1" })));
            configure.AddOpenTelemetry(options
                => options.ConfigureResource(
                    r => r.AddAttributes(new Dictionary<string, object>() { ["key2"] = "value2" })));

            configure.AddOpenTelemetry(options
                => options.ConfigureProvider((sp, p) => provider = p));
        });

        using ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

        Assert.NotNull(loggerFactory);

        Assert.NotNull(provider);

        Assert.Contains(provider!.Resource.Attributes, kvp => kvp.Key == "key1" && (string)kvp.Value == "value1");
        Assert.Contains(provider!.Resource.Attributes, kvp => kvp.Key == "key2" && (string)kvp.Value == "value2");
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryWithProviderTest()
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
    public void LoggingBuilderAddOpenTelemetryWithProviderAndDisposeSpecifiedTests(bool dispose)
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
    public void LoggingBuilderAddOpenTelemetryServicesAvailableTest()
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
    public void LoggingBuilderAddOpenTelemetryProcessorThroughDependencyTest()
    {
        CustomProcessor.InstanceCount = 0;

        var services = new ServiceCollection();

        services.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(options =>
            {
                options.AddProcessor<CustomProcessor>();
            });
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

        services.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(options =>
            {
                options.ConfigureProvider((sp, provider) =>
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
    public void LoggingBuilderAddOpenTelemetryOptionsOrderingTest()
    {
        int configureInvocationCount = 0;

        var services = new ServiceCollection();

        OpenTelemetryLoggerProvider? provider = null;

        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            // Note: This will be applied first to the final options
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.ParseStateValues = true;

            options.AddProcessor(new CustomProcessor(0));

            options.ConfigureProvider((sp, p) =>
            {
                Assert.Null(provider);
                provider = p;
                configureInvocationCount++;
            });
        });

        services.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(options =>
            {
                // Note: This will run first, but be applied second to the final options
                options.IncludeFormattedMessage = false;
                options.ParseStateValues = false;

                options.AddProcessor(new CustomProcessor(1));

                options.ConfigureProvider((sp, p) =>
                {
                    configureInvocationCount++;

                    Assert.NotNull(provider);
                    Assert.Equal(provider, p);
                });
            });
        });

        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            // Note: This will be applied last to the final options
            options.ParseStateValues = true;

            options.AddProcessor(new CustomProcessor(2));

            options.ConfigureProvider((sp, p) =>
            {
                configureInvocationCount++;

                Assert.NotNull(provider);
                Assert.Equal(provider, p);
            });
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        Assert.NotNull(provider);
        Assert.Equal(3, configureInvocationCount);

        var finalOptions = serviceProvider.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().CurrentValue;

        Assert.False(finalOptions.IncludeFormattedMessage);
        Assert.True(finalOptions.IncludeScopes);
        Assert.True(finalOptions.ParseStateValues);

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

        OpenTelemetryLoggerProvider? provider = null;

        services.AddLogging(configure =>
        {
            configure.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("Examples.LoggingExtensions"));

                options.ConfigureProvider((sp, p) => provider = p);
            });
        });

        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            options.ConfigureResource(builder => builder.AddAttributes(new Dictionary<string, object> { ["key1"] = "value1" }));
        });

        services.Configure<OpenTelemetryLoggerOptions>(options =>
        {
            options.ConfigureResource(builder => builder.AddAttributes(new Dictionary<string, object> { ["key2"] = "value2" }));
        });

        using var serviceProvider = services.BuildServiceProvider();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        Assert.NotNull(provider);

        var resource = provider!.Resource;

        Assert.NotNull(resource);

        Assert.Contains(resource.Attributes, kvp => kvp.Key == "service.name");
        Assert.Contains(resource.Attributes, kvp => kvp.Key == "service.instance.id");
        Assert.Contains(resource.Attributes, kvp => kvp.Key == "key1");
        Assert.Contains(resource.Attributes, kvp => kvp.Key == "key2");
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryAddExporterTest()
    {
        var builder = Sdk.CreateLoggerProviderBuilder();

        builder.AddExporter(ExportProcessorType.Simple, new CustomExporter());
        builder.AddExporter<CustomExporter>(ExportProcessorType.Batch);

        using var provider = builder.Build();

        Assert.NotNull(provider);

        var processor = provider.Processor as CompositeProcessor<LogRecord>;

        Assert.NotNull(processor);

        var firstProcessor = processor!.Head.Value;
        var secondProcessor = processor.Head.Next?.Value;

        Assert.True(firstProcessor is SimpleLogRecordExportProcessor simpleProcessor && simpleProcessor.Exporter is CustomExporter);
        Assert.True(secondProcessor is BatchLogRecordExportProcessor batchProcessor && batchProcessor.Exporter is CustomExporter);
    }

    [Fact]
    public void LoggingBuilderAddOpenTelemetryAddExporterWithOptionsTest()
    {
        int optionsInvocations = 0;

        var builder = Sdk.CreateLoggerProviderBuilder();

        builder.ConfigureServices(services =>
        {
            services.Configure<BatchExportLogRecordProcessorOptions>(options =>
            {
                // Note: This is testing options integration

                optionsInvocations++;

                options.MaxExportBatchSize = 18;
            });
        });

        builder.AddExporter(
            ExportProcessorType.Simple,
            new CustomExporter(),
            options =>
            {
                // Note: Options delegate isn't invoked for simple processor type
                Assert.True(false);
            });
        builder.AddExporter<CustomExporter>(
            ExportProcessorType.Batch,
            options =>
            {
                optionsInvocations++;

                Assert.Equal(18, options.BatchExportProcessorOptions.MaxExportBatchSize);

                options.BatchExportProcessorOptions.MaxExportBatchSize = 100;
            });

        using var provider = builder.Build();

        Assert.NotNull(provider);

        Assert.Equal(2, optionsInvocations);

        var processor = provider.Processor as CompositeProcessor<LogRecord>;

        Assert.NotNull(processor);

        var firstProcessor = processor!.Head.Value;
        var secondProcessor = processor.Head.Next?.Value;

        Assert.True(firstProcessor is SimpleLogRecordExportProcessor simpleProcessor && simpleProcessor.Exporter is CustomExporter);
        Assert.True(secondProcessor is BatchLogRecordExportProcessor batchProcessor
            && batchProcessor.Exporter is CustomExporter
            && batchProcessor.MaxExportBatchSize == 100);
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

    private sealed class CustomExporter : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            return ExportResult.Success;
        }
    }

    private sealed class TestClass
    {
    }
}
