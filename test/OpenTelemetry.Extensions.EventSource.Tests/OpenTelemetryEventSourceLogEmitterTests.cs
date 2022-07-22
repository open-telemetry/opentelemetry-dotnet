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
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using Microsoft.Extensions.Logging;
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
        [InlineData("OpenTelemetry.Extensions.EventSource.Tests", EventLevel.LogAlways, 2)]
        [InlineData("OpenTelemetry.Extensions.EventSource.Tests", EventLevel.Warning, 1)]
        [InlineData("_invalid_", EventLevel.LogAlways, 0)]
        public void OpenTelemetryEventSourceLogEmitterFilterTests(string sourceName, EventLevel? eventLevel, int expectedNumberOfLogRecords)
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
                (name) => name == sourceName ? eventLevel : null))
            {
                TestEventSource.Log.SimpleEvent();
                TestEventSource.Log.ComplexEvent("Test_Message", 18);
            }

            Assert.Equal(expectedNumberOfLogRecords, exportedItems.Count);
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

        [Fact]
        public void OpenTelemetryEventSourceLogEmitterSimpleEventTest()
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
                (name) => name == "OpenTelemetry.Extensions.EventSource.Tests" ? EventLevel.LogAlways : null))
            {
                TestEventSource.Log.SimpleEvent();
            }

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotEqual(DateTime.MinValue, logRecord.Timestamp);
            Assert.Equal(TestEventSource.SimpleEventMessage, logRecord.FormattedMessage);
            Assert.Equal(TestEventSource.SimpleEventId, logRecord.EventId.Id);
            Assert.Equal(nameof(TestEventSource.SimpleEvent), logRecord.EventId.Name);
            Assert.Equal(LogLevel.Warning, logRecord.LogLevel);
            Assert.Null(logRecord.CategoryName);
            Assert.Null(logRecord.Exception);

            Assert.Equal(default, logRecord.TraceId);
            Assert.Equal(default, logRecord.SpanId);
            Assert.Null(logRecord.TraceState);
            Assert.Equal(ActivityTraceFlags.None, logRecord.TraceFlags);

            Assert.NotNull(logRecord.StateValues);
            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "event_source.name" && (string?)kvp.Value == "OpenTelemetry.Extensions.EventSource.Tests");
        }

        [Fact]
        public void OpenTelemetryEventSourceLogEmitterSimpleEventWithActivityTest()
        {
            using var activity = new Activity("Test");
            activity.Start();

            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new WrappedOpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            using (var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
                openTelemetryLoggerProvider,
                (name) => name == "OpenTelemetry.Extensions.EventSource.Tests" ? EventLevel.LogAlways : null))
            {
                TestEventSource.Log.SimpleEvent();
            }

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotEqual(default, logRecord.TraceId);

            Assert.Equal(activity.TraceId, logRecord.TraceId);
            Assert.Equal(activity.SpanId, logRecord.SpanId);
            Assert.Equal(activity.TraceStateString, logRecord.TraceState);
            Assert.Equal(activity.ActivityTraceFlags, logRecord.TraceFlags);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void OpenTelemetryEventSourceLogEmitterComplexEventTest(bool formatMessage)
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new WrappedOpenTelemetryLoggerProvider(options =>
            {
                options.IncludeFormattedMessage = formatMessage;
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            using (var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
                openTelemetryLoggerProvider,
                (name) => name == "OpenTelemetry.Extensions.EventSource.Tests" ? EventLevel.LogAlways : null))
            {
                TestEventSource.Log.ComplexEvent("Test_Message", 18);
            }

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotEqual(DateTime.MinValue, logRecord.Timestamp);
            if (!formatMessage)
            {
                Assert.Equal(TestEventSource.ComplexEventMessageStructured, logRecord.FormattedMessage);
            }
            else
            {
                string expectedMessage = string.Format(CultureInfo.InvariantCulture, TestEventSource.ComplexEventMessage, "Test_Message", 18);
                Assert.Equal(expectedMessage, logRecord.FormattedMessage);
            }

            Assert.Equal(TestEventSource.ComplexEventId, logRecord.EventId.Id);
            Assert.Equal(nameof(TestEventSource.ComplexEvent), logRecord.EventId.Name);
            Assert.Equal(LogLevel.Information, logRecord.LogLevel);
            Assert.Null(logRecord.CategoryName);
            Assert.Null(logRecord.Exception);

            Assert.Equal(default, logRecord.TraceId);
            Assert.Equal(default, logRecord.SpanId);
            Assert.Null(logRecord.TraceState);
            Assert.Equal(ActivityTraceFlags.None, logRecord.TraceFlags);

            Assert.NotNull(logRecord.StateValues);
            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "event_source.name" && (string?)kvp.Value == "OpenTelemetry.Extensions.EventSource.Tests");
            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "arg1" && (string?)kvp.Value == "Test_Message");
            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "arg2" && (int?)kvp.Value == 18);
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

            public const int ComplexEventId = 2;
            public const string ComplexEventMessage = "Information event with two arguments: '{0}' & '{1}'.";
            public const string ComplexEventMessageStructured = "Information event with two arguments: '{arg1}' & '{arg2}'.";

            public static TestEventSource Log { get; } = new();

            [Event(SimpleEventId, Message = SimpleEventMessage, Level = EventLevel.Warning)]
            public void SimpleEvent()
            {
                this.WriteEvent(SimpleEventId);
            }

            [Event(ComplexEventId, Message = ComplexEventMessage, Level = EventLevel.Informational)]
            public void ComplexEvent(string arg1, int arg2)
            {
                this.WriteEvent(ComplexEventId, arg1, arg2);
            }
        }
    }
}
