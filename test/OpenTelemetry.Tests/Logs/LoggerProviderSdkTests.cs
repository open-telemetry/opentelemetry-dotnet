// <copyright file="LoggerProviderSdkTests.cs" company="OpenTelemetry Authors">
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
                    new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["OTEL_SERVICE_NAME"] = "TestServiceName" }).Build());
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

        provider.AddProcessor(new BatchLogRecordExportProcessor(new InMemoryExporter<LogRecord>(exportedItems)));

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
            .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider2);

        Assert.Equal(LogRecordThreadStaticPool.Instance, provider2.LogRecordPool);

        using var provider3 = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
            .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
            .Build() as LoggerProviderSdk;

        Assert.NotNull(provider3);

        Assert.Equal(LogRecordThreadStaticPool.Instance, provider3.LogRecordPool);
    }

    [Fact]
    public void SharedPoolUsedByProviderTests()
    {
        using var provider1 = Sdk.CreateLoggerProviderBuilder()
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
            .AddProcessor(new CompositeProcessor<LogRecord>(new BaseProcessor<LogRecord>[]
            {
                    new SimpleLogRecordExportProcessor(new NoopExporter()),
                    new BatchLogRecordExportProcessor(new NoopExporter()),
            }))
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

        provider.AddProcessor(new NoopProcessor());

        Assert.NotNull(provider.Processor);
        Assert.True(provider.Processor is NoopProcessor);

        provider.AddProcessor(new NoopProcessor());

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
