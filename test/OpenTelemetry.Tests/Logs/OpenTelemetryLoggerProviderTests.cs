// <copyright file="OpenTelemetryLoggerProviderTests.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs.Tests
{
    public sealed class OpenTelemetryLoggerProviderTests
    {
        [Fact]
        public void ThreadStaticPoolUsedByProviderTests()
        {
            using var provider1 = new OpenTelemetryLoggerProvider(new OpenTelemetryLoggerOptions());

            Assert.Equal(LogRecordThreadStaticPool.Instance, provider1.LogRecordPool);

            var options = new OpenTelemetryLoggerOptions();
            options.AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()));

            using var provider2 = new OpenTelemetryLoggerProvider(options);

            Assert.Equal(LogRecordThreadStaticPool.Instance, provider2.LogRecordPool);

            options.AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()));

            using var provider3 = new OpenTelemetryLoggerProvider(options);

            Assert.Equal(LogRecordThreadStaticPool.Instance, provider3.LogRecordPool);
        }

        [Fact]
        public void SharedPoolUsedByProviderTests()
        {
            var options = new OpenTelemetryLoggerOptions();
            options.AddProcessor(new BatchLogRecordExportProcessor(new NoopExporter()));

            using var provider1 = new OpenTelemetryLoggerProvider(options);

            Assert.Equal(LogRecordSharedPool.Current, provider1.LogRecordPool);

            options = new OpenTelemetryLoggerOptions();
            options.AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()));
            options.AddProcessor(new BatchLogRecordExportProcessor(new NoopExporter()));

            using var provider2 = new OpenTelemetryLoggerProvider(options);

            Assert.Equal(LogRecordSharedPool.Current, provider2.LogRecordPool);

            options = new OpenTelemetryLoggerOptions();
            options.AddProcessor(new SimpleLogRecordExportProcessor(new NoopExporter()));
            options.AddProcessor(new CompositeProcessor<LogRecord>(new BaseProcessor<LogRecord>[]
            {
                new SimpleLogRecordExportProcessor(new NoopExporter()),
                new BatchLogRecordExportProcessor(new NoopExporter()),
            }));

            using var provider3 = new OpenTelemetryLoggerProvider(options);

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
