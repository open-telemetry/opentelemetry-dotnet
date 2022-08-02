// <copyright file="LogEmitterTests.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Logging;
using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class LogEmitterTests
    {
        [Fact]
        public void LogEmitterBasicTest()
        {
            var exportedItems = new List<LogRecord>();

            using var provider = new OpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });

            var logEmitter = provider.CreateEmitter();

            Exception ex = new InvalidOperationException();

            logEmitter.Emit(
                new()
                {
                    CategoryName = "LogEmitter",
                    Message = "Hello world",
                    LogLevel = LogLevel.Warning,
                    EventId = new EventId(18, "CustomEvent"),
                    Exception = ex,
                },
                new()
                {
                    ["key1"] = "value1",
                    ["key2"] = "value2",
                });

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotNull(logRecord);
            Assert.Equal("LogEmitter", logRecord.CategoryName);
            Assert.Equal("Hello world", logRecord.FormattedMessage);
            Assert.Equal(LogLevel.Warning, logRecord.LogLevel);
            Assert.Equal(18, logRecord.EventId.Id);
            Assert.Equal("CustomEvent", logRecord.EventId.Name);
            Assert.Equal(ex, logRecord.Exception);
            Assert.NotEqual(DateTime.MinValue, logRecord.Timestamp);

            Assert.Equal(default, logRecord.TraceId);
            Assert.Equal(default, logRecord.SpanId);
            Assert.Equal(ActivityTraceFlags.None, logRecord.TraceFlags);
            Assert.Null(logRecord.TraceState);

            Assert.NotNull(logRecord.StateValues);
            Assert.Equal(2, logRecord.StateValues.Count);
            Assert.Contains(logRecord.StateValues, item => item.Key == "key1" && (string)item.Value == "value1");
            Assert.Contains(logRecord.StateValues, item => item.Key == "key2" && (string)item.Value == "value2");
        }

        [Fact]
        public void LogEmitterFromActivityTest()
        {
            var exportedItems = new List<LogRecord>();

            using var provider = new OpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });

            var logEmitter = provider.CreateEmitter();

            using var activity = new Activity("Test");

            activity.Start();

            activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
            activity.TraceStateString = "key1=value1";

            logEmitter.Emit(new(activity));

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotNull(logRecord);

            Assert.Equal(activity.TraceId, logRecord.TraceId);
            Assert.Equal(activity.SpanId, logRecord.SpanId);
            Assert.Equal(activity.ActivityTraceFlags, logRecord.TraceFlags);
            Assert.Equal(activity.TraceStateString, logRecord.TraceState);

            Assert.Null(logRecord.StateValues);
        }

        [Fact]
        public void LogEmitterLocalToUtcTimestampTest()
        {
            var exportedItems = new List<LogRecord>();

            using var provider = new OpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });

            var logEmitter = provider.CreateEmitter();

            DateTime timestamp = DateTime.SpecifyKind(
                new DateTime(2022, 6, 30, 16, 0, 0),
                DateTimeKind.Local);

            logEmitter.Emit(new()
            {
                Timestamp = timestamp,
            });

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotNull(logRecord);

            Assert.Equal(timestamp.ToUniversalTime(), logRecord.Timestamp);
            Assert.Equal(DateTimeKind.Utc, logRecord.Timestamp.Kind);
        }

        [Fact]
        public void LogEmitterUnspecifiedTimestampTest()
        {
            var exportedItems = new List<LogRecord>();

            using var provider = new OpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });

            var logEmitter = provider.CreateEmitter();

            DateTime timestamp = DateTime.SpecifyKind(
                new DateTime(2022, 6, 30, 16, 0, 0),
                DateTimeKind.Unspecified);

            logEmitter.Emit(new()
            {
                Timestamp = timestamp,
            });

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotNull(logRecord);

            Assert.Equal(timestamp, logRecord.Timestamp);
            Assert.Equal(DateTimeKind.Unspecified, logRecord.Timestamp.Kind);
        }
    }
}
