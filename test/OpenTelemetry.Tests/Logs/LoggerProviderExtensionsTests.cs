// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public class LoggerProviderExtensionsTests
{
    [Fact]
    public void AddProcessorTest()
    {
        using var provider = Sdk.CreateLoggerProviderBuilder()
            .Build();

        Assert.NotNull(provider);

        var providerSdk = provider as LoggerProviderSdk;

        Assert.NotNull(providerSdk);

        Assert.Null(providerSdk.Processor);

#pragma warning disable CA2000 // Dispose objects before losing scope
        provider.AddProcessor(new TestProcessor());
#pragma warning restore CA2000 // Dispose objects before losing scope

        Assert.NotNull(providerSdk.Processor);
    }

    [Fact]
    public void ForceFlushTest()
    {
        List<LogRecord> exportedItems = new();
        using var provider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(
#pragma warning disable CA2000 // Dispose objects before losing scope
                new BatchLogRecordExportProcessor(
                    new InMemoryExporter<LogRecord>(exportedItems),
#pragma warning restore CA2000 // Dispose objects before losing scope
                    scheduledDelayMilliseconds: int.MaxValue))
            .Build();

        Assert.NotNull(provider);

        var providerSdk = provider as LoggerProviderSdk;

        Assert.NotNull(providerSdk);

        var logger = providerSdk.GetLogger();

        Assert.NotNull(logger);

        logger.EmitLog(new LogRecordData { Body = "Hello world" });

        Assert.Empty(exportedItems);

        Assert.True(provider.ForceFlush());

        Assert.Single(exportedItems);
    }

    [Fact]
    public void ShutdownTest()
    {
        using var provider = Sdk.CreateLoggerProviderBuilder()
            .Build();

        Assert.NotNull(provider);

        var providerSdk = provider as LoggerProviderSdk;

        Assert.NotNull(providerSdk);

        Assert.Equal(0, providerSdk.ShutdownCount);

        Assert.True(provider.Shutdown());

        Assert.Equal(1, providerSdk.ShutdownCount);
    }

    private sealed class TestProcessor : BaseProcessor<LogRecord>
    {
    }
}
