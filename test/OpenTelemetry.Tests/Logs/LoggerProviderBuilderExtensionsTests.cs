// <copyright file="LoggerProviderBuilderExtensionsTests.cs" company="OpenTelemetry Authors">
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
            .AddProcessor(sp => new CustomProcessor())
            .AddProcessor(new CustomProcessor())
            .Build() as LoggerProviderSdk)
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

                processors.Add(processor);
                Assert.False(processor.Disposed);

                current = current.Next;
            }
        }

        Assert.Equal(3, processors.Count);

        foreach (var processor in processors)
        {
            Assert.True(processor.Disposed);
        }
    }

    [Fact]
    public void LoggerProviderBuilderCustomImplementationBuildTest()
    {
        var builder = new CustomLoggerProviderBuilder();

        var provider = builder.Build();

        Assert.NotNull(provider);
        Assert.True(provider is not LoggerProviderSdk);
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
