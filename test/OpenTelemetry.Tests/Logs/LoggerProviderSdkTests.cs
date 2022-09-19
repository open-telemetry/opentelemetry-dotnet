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

using System.Collections.Generic;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class LoggerProviderSdkTests
    {
        [Fact]
        public void ConfigureCtorTests()
        {
            using var provider = Sdk.CreateLoggerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder
                    .CreateEmpty()
                    .AddAttributes(new[] { new KeyValuePair<string, object>("key1", "value1") }))
                .AddInMemoryExporter(new List<LogRecord>())
                .Build() as LoggerProviderSdk;

            Assert.NotNull(provider);

            Assert.NotNull(provider.Processor);
            Assert.NotNull(provider.Resource);
            Assert.Contains(provider.Resource.Attributes, value => value.Key == "key1" && (string)value.Value == "value1");
        }

        [Fact]
        public void ForceFlushTest()
        {
            using var provider = Sdk.CreateLoggerProviderBuilder().Build() as LoggerProviderSdk;

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

            Assert.Equal(LogRecordThreadStaticPool.Instance, provider1.LogRecordPool);

            using var provider2 = Sdk.CreateLoggerProviderBuilder()
                .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
                .Build() as LoggerProviderSdk;

            Assert.Equal(LogRecordThreadStaticPool.Instance, provider2.LogRecordPool);

            using var provider3 = Sdk.CreateLoggerProviderBuilder()
                .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
                .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
                .Build() as LoggerProviderSdk;

            Assert.Equal(LogRecordThreadStaticPool.Instance, provider3.LogRecordPool);
        }

        [Fact]
        public void SharedPoolUsedByProviderTests()
        {
            using var provider1 = Sdk.CreateLoggerProviderBuilder()
                .AddProcessor(new BatchLogRecordExportProcessor(new NoopExporter()))
                .Build() as LoggerProviderSdk;

            Assert.Equal(LogRecordSharedPool.Current, provider1.LogRecordPool);

            using var provider2 = Sdk.CreateLoggerProviderBuilder()
                .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
                .AddProcessor(new BatchLogRecordExportProcessor(new NoopExporter()))
                .Build() as LoggerProviderSdk;

            Assert.Equal(LogRecordSharedPool.Current, provider2.LogRecordPool);

            using var provider3 = Sdk.CreateLoggerProviderBuilder()
                .AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()))
                .AddProcessor(new CompositeProcessor<LogRecord>(new BaseProcessor<LogRecord>[]
                {
                    new SimpleLogRecordExportProcessor(new NoopExporter()),
                    new BatchLogRecordExportProcessor(new NoopExporter()),
                }))
                .Build() as LoggerProviderSdk;

            Assert.Equal(LogRecordSharedPool.Current, provider3.LogRecordPool);
        }

        private sealed class NoopExporter : BaseExporter<LogRecord>
        {
            public override ExportResult Export(in Batch<LogRecord> batch)
            {
                return ExportResult.Success;
            }
        }
    }
}
