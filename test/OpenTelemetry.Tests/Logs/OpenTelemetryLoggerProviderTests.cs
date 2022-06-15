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

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class OpenTelemetryLoggerProviderTests
    {
        [Fact]
        public void DefaultCtorTests()
        {
            OpenTelemetryLoggerOptions defaults = new();

            using OpenTelemetryLoggerProvider provider = new();

            Assert.Equal(defaults.IncludeScopes, provider.IncludeScopes);
            Assert.Equal(defaults.IncludeFormattedMessage, provider.IncludeFormattedMessage);
            Assert.Equal(defaults.ParseStateValues, provider.ParseStateValues);
            Assert.Null(provider.Processor);
            Assert.NotNull(provider.Resource);
        }

        [Fact]
        public void ConfigureCtorTests()
        {
            OpenTelemetryLoggerOptions defaults = new();

            using OpenTelemetryLoggerProvider provider = new(options =>
            {
                options.IncludeScopes = !defaults.IncludeScopes;
                options.IncludeFormattedMessage = !defaults.IncludeFormattedMessage;
                options.ParseStateValues = !defaults.ParseStateValues;

                options.SetResourceBuilder(ResourceBuilder
                    .CreateEmpty()
                    .AddAttributes(new[] { new KeyValuePair<string, object>("key1", "value1") }));

                options.AddInMemoryExporter(new List<LogRecord>());
            });

            Assert.Equal(!defaults.IncludeScopes, provider.IncludeScopes);
            Assert.Equal(!defaults.IncludeFormattedMessage, provider.IncludeFormattedMessage);
            Assert.Equal(!defaults.ParseStateValues, provider.ParseStateValues);
            Assert.NotNull(provider.Processor);
            Assert.NotNull(provider.Resource);
            Assert.Contains(provider.Resource.Attributes, value => value.Key == "key1" && (string)value.Value == "value1");
        }

        [Fact]
        public void ForceFlushTest()
        {
            using OpenTelemetryLoggerProvider provider1 = new();
            Assert.True(provider1.ForceFlush());

            var exporter = new TestExporter();

            using OpenTelemetryLoggerProvider provider2 = new(options => options
                .AddProcessor(new BatchLogRecordExportProcessor(exporter)));

            var logger = provider2.CreateLogger("TestLogger");

            logger.LogInformation("hello world");

            Assert.Empty(exporter.ExportedItems);

            Assert.True(provider2.ForceFlush());

            Assert.Single(exporter.ExportedItems);
        }

        private sealed class TestExporter : BaseExporter<LogRecord>
        {
            public List<LogRecord> ExportedItems { get; } = new();

            public override ExportResult Export(in Batch<LogRecord> batch)
            {
                foreach (LogRecord logRecord in batch)
                {
                    this.ExportedItems.Add(logRecord);
                }

                return ExportResult.Success;
            }
        }
    }
}
