// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Logs.Tests;

public sealed class LoggerProviderSdkTests
{
    [Fact]
    public void ResourceDetectionUsingIConfigurationTest()
    {
        using var provider = Sdk.CreateLoggerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(
                    new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_SERVICE_NAME"] = "TestServiceName" }).Build());
            })
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider);

        Assert.Contains(provider.Resource.Attributes, kvp => kvp.Key == "service.name" && (string)kvp.Value == "TestServiceName");
    }

    [Fact]
    public void ForceFlushTest()
    {
        using var provider = Sdk.CreateLoggerProviderBuilder().Build() as LoggerProviderSdk;

        Assert.NotNull(provider);

        Assert.True(provider.ForceFlush());

        List<LogRecord> exportedItems = [];

#pragma warning disable CA2000 // Dispose objects before losing scope
        provider.AddProcessor(new BatchLogRecordExportProcessor(new InMemoryExporter<LogRecord>(exportedItems)));
#pragma warning restore CA2000 // Dispose objects before losing scope

        var logger = provider.GetLogger("TestLogger");

        logger.EmitLog(new() { Body = "Hello world" });

        Assert.Empty(exportedItems);

        Assert.True(provider.ForceFlush());

        Assert.Single(exportedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmitLogRespectsSuppressInstrumentationTest(bool includeAttributes)
    {
        List<LogRecord> exportedItems = [];

        using var provider = Sdk.CreateLoggerProviderBuilder()
            .AddInMemoryExporter(exportedItems)
            .Build();

        var logger = provider.GetLogger("TestLogger");

        using (SuppressInstrumentationScope.Begin())
        {
            if (includeAttributes)
            {
                logger.EmitLog(new() { Body = "Suppressed" }, new() { { "key", "value" } });
            }
            else
            {
                logger.EmitLog(new() { Body = "Suppressed" });
            }
        }

        Assert.Empty(exportedItems);

        logger.EmitLog(new() { Body = "Hello world" });

        var logRecord = Assert.Single(exportedItems);
        Assert.Equal("Hello world", logRecord.Body);
    }

    [Theory]
    [InlineData(ExportResult.Success)]
    [InlineData(ExportResult.Failure)]
    public void EmitLogDuringExportIsNotFedBackIntoPipelineTest(ExportResult exportResult)
    {
        Logger? logger = null;
        var exportCount = 0;

        using var provider = Sdk.CreateLoggerProviderBuilder()
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new SimpleLogRecordExportProcessor(new DelegatingExporter<LogRecord>
            {
                OnExportFunc = batch =>
                {
                    using var scope = SuppressInstrumentationScope.Begin();

                    // Bound recursion so the regression cannot overflow the test host's stack.
                    if (++exportCount < 3)
                    {
                        logger!.EmitLog(new() { Body = "Emitted during export" });
                    }

                    return exportResult;
                },
            }))
#pragma warning restore CA2000 // Dispose objects before losing scope
            .Build();

        logger = provider.GetLogger("TestLogger");

        logger.EmitLog(new() { Body = "Hello world" });

        Assert.Equal(1, exportCount);
    }

    [Fact]
    public void ThreadStaticPoolUsedByProviderTests()
    {
        using var provider1 = Sdk.CreateLoggerProviderBuilder().Build() as LoggerProviderSdk;

        Assert.NotNull(provider1);

        Assert.Equal(LogRecordThreadStaticPool.Instance, provider1.LogRecordPool);

        using var provider2 = Sdk.CreateLoggerProviderBuilder()
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
#pragma warning restore CA2000 // Dispose objects before losing scope
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider2);

        Assert.Equal(LogRecordThreadStaticPool.Instance, provider2.LogRecordPool);

        using var provider3 = Sdk.CreateLoggerProviderBuilder()
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
            .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
#pragma warning restore CA2000 // Dispose objects before losing scope
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider3);

        Assert.Equal(LogRecordThreadStaticPool.Instance, provider3.LogRecordPool);
    }

    [Fact]
    public void SharedPoolUsedByProviderTests()
    {
        using var provider1 = Sdk.CreateLoggerProviderBuilder()
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new BatchLogRecordExportProcessor(new NoopExporter()))
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider1);

        Assert.Equal(LogRecordSharedPool.Current, provider1.LogRecordPool);

        using var provider2 = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
            .AddProcessor(new BatchLogRecordExportProcessor(new NoopExporter()))
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider2);

        Assert.Equal(LogRecordSharedPool.Current, provider2.LogRecordPool);

        using var provider3 = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
            .AddProcessor(new CompositeProcessor<LogRecord>(
            [
                    new SimpleLogRecordExportProcessor(new NoopExporter()),
                    new BatchLogRecordExportProcessor(new NoopExporter()),
#pragma warning restore CA2000 // Dispose objects before losing scope
            ]))
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider3);

        Assert.Equal(LogRecordSharedPool.Current, provider3.LogRecordPool);
    }

    [Fact]
    public void AddProcessorTest()
    {
        using var provider = Sdk.CreateLoggerProviderBuilder()
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider);
        Assert.Null(provider.Processor);

#pragma warning disable CA2000 // Dispose objects before losing scope
        provider.AddProcessor(new NoopProcessor());
#pragma warning restore CA2000 // Dispose objects before losing scope

        Assert.NotNull(provider.Processor);
        Assert.True(provider.Processor is NoopProcessor);

#pragma warning disable CA2000 // Dispose objects before losing scope
        provider.AddProcessor(new NoopProcessor());
#pragma warning restore CA2000 // Dispose objects before losing scope

        Assert.NotNull(provider.Processor);
        Assert.True(provider.Processor is CompositeProcessor<LogRecord>);
    }

    [Fact]
    public void BuilderTypeDoesNotChangeTest()
    {
        var originalBuilder = Sdk.CreateLoggerProviderBuilder();
        var currentBuilder = originalBuilder;

        var deferredBuilder = currentBuilder as IDeferredLoggerProviderBuilder;
        Assert.NotNull(deferredBuilder);

        currentBuilder = deferredBuilder.Configure((sp, innerBuilder) => { });
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.ConfigureServices(s => { });
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.AddInstrumentation(() => new object());
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        using var provider = currentBuilder.Build();

        Assert.NotNull(provider);
    }

    private sealed class NoopProcessor : BaseProcessor<LogRecord>
    {
    }

    private sealed class NoopExporter : BaseExporter<LogRecord>
    {
        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            return ExportResult.Success;
        }
    }
}
