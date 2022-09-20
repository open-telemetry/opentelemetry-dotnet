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
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class LoggerProviderBuilderExtensionsTests
{
    [Fact]
    public void LoggerProviderBuilderAddExporterTest()
    {
        var builder = Sdk.CreateLoggerProviderBuilder();

        builder.AddExporter(ExportProcessorType.Simple, new CustomExporter());
        builder.AddExporter<CustomExporter>(ExportProcessorType.Batch);

        using var provider = builder.Build() as LoggerProviderSdk;

        Assert.NotNull(provider);

        var processor = provider.Processor as CompositeProcessor<LogRecord>;

        Assert.NotNull(processor);

        var firstProcessor = processor!.Head.Value;
        var secondProcessor = processor.Head.Next?.Value;

        Assert.True(firstProcessor is SimpleLogRecordExportProcessor simpleProcessor && simpleProcessor.Exporter is CustomExporter);
        Assert.True(secondProcessor is BatchLogRecordExportProcessor batchProcessor && batchProcessor.Exporter is CustomExporter);
    }

    [Fact]
    public void LoggerProviderBuilderAddExporterWithOptionsTest()
    {
        int optionsInvocations = 0;

        var builder = Sdk.CreateLoggerProviderBuilder();

        builder.ConfigureServices(services =>
        {
            services.Configure<ExportLogRecordProcessorOptions>(options =>
            {
                // Note: This is testing options integration

                optionsInvocations++;

                options.BatchExportProcessorOptions.MaxExportBatchSize = 18;
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

        using var provider = builder.Build() as LoggerProviderSdk;

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

    [Fact]
    public void LoggerProviderBuilderAddExporterNamedOptionsTest()
    {
        var builder = Sdk.CreateLoggerProviderBuilder();

        int defaultOptionsConfigureInvocations = 0;
        int namedOptionsConfigureInvocations = 0;

        builder.ConfigureServices(services =>
        {
            services.Configure<ExportLogRecordProcessorOptions>(o => defaultOptionsConfigureInvocations++);

            services.Configure<ExportLogRecordProcessorOptions>("Exporter2", o => namedOptionsConfigureInvocations++);
        });

        builder.AddExporter(ExportProcessorType.Batch, new CustomExporter());
        builder.AddExporter(ExportProcessorType.Batch, new CustomExporter(), name: "Exporter2", configure: null);
        builder.AddExporter<CustomExporter>(ExportProcessorType.Batch);
        builder.AddExporter<CustomExporter>(ExportProcessorType.Batch, name: "Exporter2", configure: null);

        using var provider = builder.Build();

        Assert.NotNull(provider);

        Assert.Equal(1, defaultOptionsConfigureInvocations);
        Assert.Equal(1, namedOptionsConfigureInvocations);
    }

    [Fact]
    public void LoggerProviderBuilderAddInstrumentationTest()
    {
        List<object>? instrumentation = null;

        using (var provider = Sdk.CreateLoggerProviderBuilder()
            .AddInstrumentation<CustomInstrumentation>()
            .AddInstrumentation((sp, provider) => new CustomInstrumentation(provider))
            .Build() as LoggerProviderSdk)
        {
            Assert.NotNull(provider);

            Assert.Equal(2, provider.Instrumentations.Count);

            Assert.Null(((CustomInstrumentation)provider.Instrumentations[0]).Provider);
            Assert.False(((CustomInstrumentation)provider.Instrumentations[0]).Disposed);

            Assert.NotNull(((CustomInstrumentation)provider.Instrumentations[1]).Provider);
            Assert.False(((CustomInstrumentation)provider.Instrumentations[1]).Disposed);

            instrumentation = new List<object>(provider.Instrumentations);
        }

        Assert.True(((CustomInstrumentation)instrumentation[0]).Disposed);
        Assert.True(((CustomInstrumentation)instrumentation[1]).Disposed);
    }

    private sealed class CustomInstrumentation : IDisposable
    {
        public bool Disposed;
        public LoggerProvider? Provider;

        public CustomInstrumentation()
        {
        }

        public CustomInstrumentation(LoggerProvider provider)
        {
            this.Provider = provider;
        }

        public void Dispose()
        {
            this.Disposed = true;
        }
    }

    private sealed class CustomExporter : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            return ExportResult.Success;
        }
    }
}
