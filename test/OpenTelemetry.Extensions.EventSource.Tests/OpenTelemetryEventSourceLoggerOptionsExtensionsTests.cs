// <copyright file="OpenTelemetryEventSourceLoggerOptionsExtensionsTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Extensions.EventSource.Tests
{
    public class OpenTelemetryEventSourceLoggerOptionsExtensionsTests
    {
        [Fact]
        public void AddOpenTelemetryEventSourceLogEmitterTest()
        {
            var exportedItems = new List<LogRecord>();

            var services = new ServiceCollection();

            services.AddLogging(configure =>
            {
                configure.AddOpenTelemetry(options =>
                {
                    options
                        .AddInMemoryExporter(exportedItems)
                        .AddEventSourceLogEmitter((name) => name == TestEventSource.EventSourceName ? EventLevel.LogAlways : null);
                });
            });

            OpenTelemetryEventSourceLoggerOptionsExtensions.EventSourceManager? eventSourceManager = null;

            using (var serviceProvider = services.BuildServiceProvider())
            {
                var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

                eventSourceManager = serviceProvider.GetRequiredService<OpenTelemetryEventSourceLoggerOptionsExtensions.EventSourceManager>();

                Assert.Single(eventSourceManager.Emitters);

                TestEventSource.Log.SimpleEvent();
            }

            Assert.Single(exportedItems);

            Assert.Empty(eventSourceManager.Emitters);
        }
    }
}
