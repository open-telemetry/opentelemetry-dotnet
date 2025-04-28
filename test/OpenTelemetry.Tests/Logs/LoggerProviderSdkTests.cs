// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using Xunit;

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

        List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
        provider.AddProcessor(new BatchLogRecordExportProcessor(new InMemoryExporter<LogRecord>(exportedItems)));
#pragma warning restore CA2000 // Dispose objects before losing scope

        var logger = provider.GetLogger("TestLogger");

        logger.EmitLog(new() { Body = "Hello world" });

        Assert.Empty(exportedItems);

        Assert.True(provider.ForceFlush());

        Assert.Single(exportedItems);
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
