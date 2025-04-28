// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class LoggerProviderBuilderExtensionsTests
{
    [Fact]
    public void LoggerProviderBuilderAddInstrumentationTest()
    {
        List<object>? instrumentation = null;

        using (var provider = Sdk.CreateLoggerProviderBuilder()
            .AddInstrumentation<CustomInstrumentation>()
            .AddInstrumentation((sp, provider) => new CustomInstrumentation() { Provider = provider })
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddInstrumentation(new CustomInstrumentation())
#pragma warning restore CA2000 // Dispose objects before losing scope
            .AddInstrumentation(() => (object?)null)
            .Build() as LoggerProviderSdk)
        {
            Assert.NotNull(provider);

            Assert.Equal(3, provider.Instrumentations.Count);

            Assert.Null(((CustomInstrumentation)provider.Instrumentations[0]).Provider);
            Assert.False(((CustomInstrumentation)provider.Instrumentations[0]).Disposed);

            Assert.NotNull(((CustomInstrumentation)provider.Instrumentations[1]).Provider);
            Assert.False(((CustomInstrumentation)provider.Instrumentations[1]).Disposed);

            Assert.Null(((CustomInstrumentation)provider.Instrumentations[2]).Provider);
            Assert.False(((CustomInstrumentation)provider.Instrumentations[2]).Disposed);

            instrumentation = [.. provider.Instrumentations];
        }

        Assert.True(((CustomInstrumentation)instrumentation[0]).Disposed);
        Assert.True(((CustomInstrumentation)instrumentation[1]).Disposed);
        Assert.True(((CustomInstrumentation)instrumentation[2]).Disposed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LoggerProviderBuilderNestedResolutionUsingBuilderTest(bool callNestedConfigure)
    {
        bool innerConfigureBuilderTestExecuted = false;
        bool innerConfigureOpenTelemetryLoggerProviderTestExecuted = false;
        bool innerConfigureOpenTelemetryLoggerProviderTestWithServiceProviderExecuted = false;

        using var provider = Sdk.CreateLoggerProviderBuilder()
            .ConfigureServices(services =>
            {
                if (callNestedConfigure)
                {
                    services.ConfigureOpenTelemetryLoggerProvider(
                        builder =>
                        {
                            innerConfigureOpenTelemetryLoggerProviderTestExecuted = true;
                            builder.AddInstrumentation<CustomInstrumentation>();
                        });
                    services.ConfigureOpenTelemetryLoggerProvider(
                        (sp, builder) =>
                        {
                            innerConfigureOpenTelemetryLoggerProviderTestWithServiceProviderExecuted = true;
                            Assert.Throws<NotSupportedException>(() => builder.AddInstrumentation<CustomInstrumentation>());
                        });
                }
            })
            .ConfigureBuilder((sp, builder) =>
            {
                innerConfigureBuilderTestExecuted = true;
                Assert.Throws<NotSupportedException>(() => sp.GetService<LoggerProvider>());
            })
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider);

        Assert.True(innerConfigureBuilderTestExecuted);
        Assert.Equal(callNestedConfigure, innerConfigureOpenTelemetryLoggerProviderTestExecuted);
        Assert.Equal(callNestedConfigure, innerConfigureOpenTelemetryLoggerProviderTestWithServiceProviderExecuted);

        if (callNestedConfigure)
        {
            Assert.Single(provider.Instrumentations);
        }
        else
        {
            Assert.Empty(provider.Instrumentations);
        }

        Assert.Throws<NotSupportedException>(() => provider.GetServiceProvider()?.GetService<LoggerProvider>());
    }

    [Fact]
    public void LoggerProviderBuilderSetResourceBuilderTests()
    {
        using var provider = Sdk.CreateLoggerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder
                .CreateEmpty()
                .AddAttributes(new[] { new KeyValuePair<string, object>("key1", "value1") }))
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider);

        Assert.NotNull(provider.Resource);
        Assert.Contains(provider.Resource.Attributes, value => value.Key == "key1" && (string)value.Value == "value1");
    }

    [Fact]
    public void LoggerProviderBuilderConfigureResourceBuilderTests()
    {
        using var provider = Sdk.CreateLoggerProviderBuilder()
            .ConfigureResource(resource => resource
                .Clear()
                .AddAttributes(new[] { new KeyValuePair<string, object>("key1", "value1") }))
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider);

        Assert.NotNull(provider.Resource);
        Assert.Contains(provider.Resource.Attributes, value => value.Key == "key1" && (string)value.Value == "value1");
    }

    [Fact]
    public void LoggerProviderBuilderUsingDependencyInjectionTest()
    {
        using var provider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor<CustomProcessor>()
            .AddProcessor<CustomProcessor>()
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider);

        var processors = ((IServiceProvider)provider.OwnedServiceProvider!).GetServices<CustomProcessor>();

        // Note: Two "Add" calls but it is a singleton so only a single registration is produced
        Assert.Single(processors);

        var processor = provider.Processor as CompositeProcessor<LogRecord>;

        Assert.NotNull(processor);

        // Note: Two "Add" calls due yield two processors added to provider, even though they are the same
        Assert.True(processor.Head.Value is CustomProcessor);
        Assert.True(processor.Head.Next?.Value is CustomProcessor);
    }

    [Fact]
    public void LoggerProviderBuilderAddProcessorTest()
    {
        List<CustomProcessor> processorsToAdd = new()
        {
            new CustomProcessor()
            {
                Name = "A",
            },
            new CustomProcessor()
            {
                Name = "B",
            },
            new CustomProcessor()
            {
                Name = "C",
            },
        };

        var builder = Sdk.CreateLoggerProviderBuilder();
        foreach (var processor in processorsToAdd)
        {
            builder.AddProcessor(processor);
        }

        List<CustomProcessor> expectedProcessors = new()
        {
            processorsToAdd.First(p => p.Name == "A"),
            processorsToAdd.First(p => p.Name == "B"),
            processorsToAdd.First(p => p.Name == "C"),
        };

        List<CustomProcessor> actualProcessors = new();

        using (var provider = builder.Build() as LoggerProviderSdk)
        {
            Assert.NotNull(provider);
            Assert.NotNull(provider.Processor);

            var compositeProcessor = provider.Processor as CompositeProcessor<LogRecord>;

            Assert.NotNull(compositeProcessor);

            var current = compositeProcessor.Head;
            while (current != null)
            {
                var processor = current.Value as CustomProcessor;
                Assert.NotNull(processor);

                actualProcessors.Add(processor);
                Assert.False(processor.Disposed);

                current = current.Next;
            }

            Assert.Equal(expectedProcessors, actualProcessors);
        }

        foreach (var processor in actualProcessors)
        {
            Assert.True(processor.Disposed);
        }
    }

    [Fact]
    public void LoggerProviderBuilderAddProcessorWithWeightTest()
    {
        List<CustomProcessor> processorsToAdd = new()
        {
            new CustomProcessor()
            {
                Name = "C",
                PipelineWeight = 0,
            },
            new CustomProcessor()
            {
                Name = "E",
                PipelineWeight = 10_000,
            },
            new CustomProcessor()
            {
                Name = "B",
                PipelineWeight = -10_000,
            },
            new CustomProcessor()
            {
                Name = "F",
                PipelineWeight = int.MaxValue,
            },
            new CustomProcessor()
            {
                Name = "A",
                PipelineWeight = int.MinValue,
            },
            new CustomProcessor()
            {
                Name = "D",
                PipelineWeight = 0,
            },
        };

        var builder = Sdk.CreateLoggerProviderBuilder();
        foreach (var processor in processorsToAdd)
        {
            builder.AddProcessor(processor);
        }

        List<CustomProcessor> expectedProcessors = new()
        {
            processorsToAdd.First(p => p.Name == "A"),
            processorsToAdd.First(p => p.Name == "B"),
            processorsToAdd.First(p => p.Name == "C"),
            processorsToAdd.First(p => p.Name == "D"),
            processorsToAdd.First(p => p.Name == "E"),
            processorsToAdd.First(p => p.Name == "F"),
        };

        List<CustomProcessor> actualProcessors = new();

        using (var provider = builder.Build() as LoggerProviderSdk)
        {
            Assert.NotNull(provider);
            Assert.NotNull(provider.Processor);

            var compositeProcessor = provider.Processor as CompositeProcessor<LogRecord>;

            Assert.NotNull(compositeProcessor);

            var lastWeight = int.MinValue;
            var current = compositeProcessor.Head;
            while (current != null)
            {
                var processor = current.Value as CustomProcessor;
                Assert.NotNull(processor);

                actualProcessors.Add(processor);
                Assert.False(processor.Disposed);

                Assert.True(processor.PipelineWeight >= lastWeight);

                lastWeight = processor.PipelineWeight;

                current = current.Next;
            }

            Assert.Equal(expectedProcessors, actualProcessors);
        }

        foreach (var processor in actualProcessors)
        {
            Assert.True(processor.Disposed);
        }
    }

    [Fact]
    public void LoggerProviderBuilderCustomImplementationBuildTest()
    {
        var builder = new CustomLoggerProviderBuilder();

        Assert.Throws<NotSupportedException>(() => builder.Build());
    }

    private sealed class CustomInstrumentation : IDisposable
    {
        public bool Disposed;
        public LoggerProvider? Provider;

        public void Dispose()
        {
            this.Disposed = true;
        }
    }

    private sealed class CustomProcessor : BaseProcessor<LogRecord>
    {
        public string? Name;
        public bool Disposed;

        protected override void Dispose(bool disposing)
        {
            this.Disposed = true;

            base.Dispose(disposing);
        }
    }

    private sealed class CustomLoggerProviderBuilder : LoggerProviderBuilder
    {
        public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            throw new NotImplementedException();
        }
    }
}
