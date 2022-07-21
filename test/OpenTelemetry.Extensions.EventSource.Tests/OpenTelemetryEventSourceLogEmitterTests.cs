// <copyright file="OpenTelemetryEventSourceLogEmitterTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Extensions.EventSource.Tests
{
    public class OpenTelemetryEventSourceLogEmitterTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OpenTelemetryEventSourceLogEmitterDisposesProviderTests(bool dispose)
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new WrappedOpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            using (var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
                openTelemetryLoggerProvider,
                (name) => null,
                disposeProvider: dispose))
            {
            }

            Assert.Equal(dispose, openTelemetryLoggerProvider.Disposed);

            if (!dispose)
            {
                openTelemetryLoggerProvider.Dispose();
            }

            Assert.True(openTelemetryLoggerProvider.Disposed);
        }

        [Theory]
        [InlineData("OpenTelemetry.Extensions.EventSource.Tests", true)]
        [InlineData("_invalid_", false)]
        public void OpenTelemetryEventSourceLogEmitterFilterTests(string sourceName, bool shouldCaptureMessages)
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new WrappedOpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            using (var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
                openTelemetryLoggerProvider,
                (name) => name == sourceName ? EventLevel.LogAlways : null))
            {
                TestEventSource.Log.SimpleEvent();
            }

            if (shouldCaptureMessages)
            {
                Assert.Single(exportedItems);
            }
            else
            {
                Assert.Empty(exportedItems);
            }
        }

        [Fact]
        public void OpenTelemetryEventSourceLogEmitterCapturesExistingSourceTest()
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new WrappedOpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            TestEventSource.Log.SimpleEvent();

            using (var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
                openTelemetryLoggerProvider,
                (name) => name == "OpenTelemetry.Extensions.EventSource.Tests" ? EventLevel.LogAlways : null))
            {
                TestEventSource.Log.SimpleEvent();
            }

            Assert.Single(exportedItems);
        }

        private sealed class WrappedOpenTelemetryLoggerProvider : OpenTelemetryLoggerProvider
        {
            public WrappedOpenTelemetryLoggerProvider(Action<OpenTelemetryLoggerOptions> configure)
                : base(configure)
            {
            }

            public bool Disposed { get; private set; }

            protected override void Dispose(bool disposing)
            {
                this.Disposed = true;

                base.Dispose(disposing);
            }
        }

        private sealed class ComplexType
        {
            public override string ToString() => "ComplexTypeToString";
        }

        [EventSource(Name = "OpenTelemetry.Extensions.EventSource.Tests")]
        private sealed class TestEventSource : System.Diagnostics.Tracing.EventSource
        {
            public const int SimpleEventId = 1;
            public const string SimpleEventMessage = "Warning event with no arguments.";

            public static TestEventSource Log { get; } = new();

            [Event(SimpleEventId, Message = SimpleEventMessage, Level = EventLevel.Warning)]
            public void SimpleEvent()
            {
                this.WriteEvent(SimpleEventId);
            }
        }
    }
}
