// <copyright file="LoggerProviderExtensionsTests.cs" company="OpenTelemetry Authors">
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

        provider.AddProcessor(new TestProcessor());

        Assert.NotNull(providerSdk.Processor);
    }

    [Fact]
    public void ForceFlushTest()
    {
        var exporter = new TestExporter();

        using var provider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(
                new BatchLogRecordExportProcessor(
                    exporter,
                    scheduledDelayMilliseconds: int.MaxValue))
            .Build();

        Assert.NotNull(provider);

        var providerSdk = provider as LoggerProviderSdk;

        Assert.NotNull(providerSdk);

        var logger = providerSdk.GetLogger();

        Assert.NotNull(logger);

        logger.EmitLog(new LogRecordData { Body = "Hello world" });

        Assert.Empty(exporter.LogRecords);

        Assert.True(provider.ForceFlush());

        Assert.Single(exporter.LogRecords);
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

    private sealed class TestExporter : BaseExporter<LogRecord>
    {
        public List<LogRecord> LogRecords { get; } = new();

        public override ExportResult Export(in Batch<LogRecord> batch)
        {
            foreach (var logRecord in batch)
            {
                this.LogRecords.Add(logRecord);
            }

            return ExportResult.Success;
        }
    }
}
