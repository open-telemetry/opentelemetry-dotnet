// <copyright file="SimpleExportLogRecordProcessorTest.cs" company="OpenTelemetry Authors">
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

#if !NET452 && !NET46
#if NETCOREAPP2_1
using Microsoft.Extensions.DependencyInjection;
#endif
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Tests.Logs
{
    public class SimpleExportLogRecordProcessorTest : IDisposable
    {
        private readonly ILogger logger;
        private readonly List<LogRecord> exportedItems = new List<LogRecord>();
#if NETCOREAPP2_1
        private readonly ServiceProvider serviceProvider;
#else
        private readonly ILoggerFactory loggerFactory;
#endif
        private readonly BaseExportProcessor<LogRecord> processor;
        private readonly BaseExporter<LogRecord> exporter;

        public SimpleExportLogRecordProcessorTest()
        {
            this.exporter = new InMemoryExporter<LogRecord>(this.exportedItems);
            this.processor = new SimpleExportProcessor<LogRecord>(this.exporter);
#if NETCOREAPP2_1
            var serviceCollection = new ServiceCollection().AddLogging(builder =>
#else
            this.loggerFactory = LoggerFactory.Create(builder =>
#endif
            {
                builder.AddOpenTelemetry(options => options
                    .AddProcessor(this.processor));
            });

#if NETCOREAPP2_1
            this.serviceProvider = serviceCollection.BuildServiceProvider();
            this.logger = this.serviceProvider.GetRequiredService<ILogger<SimpleExportLogRecordProcessorTest>>();
#else
            this.logger = this.loggerFactory.CreateLogger<SimpleExportLogRecordProcessorTest>();
#endif
        }

        [Fact]
        public void CheckNullExporter()
        {
            Assert.Throws<ArgumentNullException>(() => new SimpleExportProcessor<LogRecord>(null));
        }

        [Fact]
        public void CheckExportedOnEnd()
        {
            this.logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(this.exportedItems);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckForceFlushExport(int timeout)
        {
            this.logger.LogInformation($"Hello from potato {0.99}.");
            this.logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

            // checking before force flush
            Assert.Equal(2, this.exportedItems.Count);

            // forcing flush
            this.processor.ForceFlush(timeout);
            Assert.Equal(2, this.exportedItems.Count);
        }

        [Theory]
        [InlineData(Timeout.Infinite)]
        [InlineData(0)]
        [InlineData(1)]
        public void CheckShutdownExport(int timeout)
        {
            this.logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

            // checking before shutdown
            Assert.Single(this.exportedItems);

            this.processor.Shutdown(timeout);
            Assert.Single(this.exportedItems);
        }

        public void Dispose()
        {
#if NETCOREAPP2_1
            this.serviceProvider?.Dispose();
#else
            this.loggerFactory?.Dispose();
#endif
        }
    }
}
#endif
