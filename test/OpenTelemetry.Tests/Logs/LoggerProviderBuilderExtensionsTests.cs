// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

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
            .AddInstrumentation(new CustomInstrumentation())
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

            instrumentation = new List<object>(provider.Instrumentations);
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
    public void LoggerProviderBuilderAddProcessorTest()
    {
        List<CustomProcessor> processors = new();

        using (var provider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor<CustomProcessor>()
            .AddProcessor(new CustomProcessor()
            {
                PipelineWeight = ProcessorPipelineWeight.PipelineExporter,
            })
            .AddProcessor(new CustomProcessor()
            {
                PipelineWeight = ProcessorPipelineWeight.PipelineEnrichment,
            })
            .AddProcessor(sp => new CustomProcessor()
            {
                PipelineWeight = ProcessorPipelineWeight.PipelineEnd,
            })
            .AddProcessor(new CustomProcessor()
            {
                PipelineWeight = ProcessorPipelineWeight.PipelineStart,
            })
            .Build() as LoggerProviderSdk)
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

                processors.Add(processor);
                Assert.False(processor.Disposed);

                Assert.True((int)processor.PipelineWeight >= lastWeight);

                lastWeight = (int)processor.PipelineWeight;

                current = current.Next;
            }
        }

        Assert.Equal(5, processors.Count);

        foreach (var processor in processors)
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
        public bool Disposed;

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

    private sealed class CustomLoggerProviderBuilder : LoggerProviderBuilder
    {
        public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            throw new NotImplementedException();
        }
    }
}
