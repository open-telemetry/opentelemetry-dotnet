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

            using var provider = Sdk.CreateLoggerProviderBuilder()
                .AddInMemoryExporter(exportedItems)
                .Build();

            var logger = provider.GetLogger("test");

            logger.EmitLog(
                new()
                {
                    Body = "Hello world",
                    Severity = LogRecordSeverity.Warning,
                },
                new()
                {
                    ["key1"] = "value1",
                    ["key2"] = "value2",
                });

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotNull(logRecord);
            Assert.Null(logRecord.CategoryName);
            Assert.Null(logRecord.FormattedMessage);
            Assert.Equal("Hello world", logRecord.Body);
            Assert.Equal(LogLevel.Warning, logRecord.LogLevel);
            Assert.Equal(LogRecordSeverity.Warning, logRecord.Severity);
            Assert.Equal(default, logRecord.EventId);
            Assert.Null(logRecord.Exception);
            Assert.NotEqual(DateTime.MinValue, logRecord.Timestamp);

            Assert.Equal(default, logRecord.TraceId);
            Assert.Equal(default, logRecord.SpanId);
            Assert.Equal(ActivityTraceFlags.None, logRecord.TraceFlags);
            Assert.Null(logRecord.TraceState);

            Assert.NotNull(logRecord.Attributes);
            Assert.Equal(2, logRecord.Attributes.Count);
            Assert.Contains(logRecord.Attributes, item => item.Key == "key1" && (string)item.Value == "value1");
            Assert.Contains(logRecord.Attributes, item => item.Key == "key2" && (string)item.Value == "value2");
        }

        [Fact]
        public void LogEmitterFromActivityTest()
        {
            var exportedItems = new List<LogRecord>();

            using var provider = Sdk.CreateLoggerProviderBuilder()
                .AddInMemoryExporter(exportedItems)
                .Build();

            var logger = provider.GetLogger();

            using var activity = new Activity("Test");

            activity.Start();

            activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
            activity.TraceStateString = "key1=value1";

            logger.EmitLog(new(activity));

            Assert.Single(exportedItems);

            var logRecord = exportedItems[0];

            Assert.NotNull(logRecord);

            Assert.Equal(activity.TraceId, logRecord.TraceId);
            Assert.Equal(activity.SpanId, logRecord.SpanId);
            Assert.Equal(activity.ActivityTraceFlags, logRecord.TraceFlags);
            Assert.Null(logRecord.TraceState);

            Assert.Null(logRecord.Attributes);
        }

        [Fact]
        public void LogEmitterLocalToUtcTimestampTest()
        {
            var exportedItems = new List<LogRecord>();

            using var provider = Sdk.CreateLoggerProviderBuilder()
                .AddInMemoryExporter(exportedItems)
                .Build();

            var logger = provider.GetLogger();

            DateTime timestamp = DateTime.SpecifyKind(
                new DateTime(2022, 6, 30, 16, 0, 0),
                DateTimeKind.Local);

            logger.EmitLog(new()
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

            using var provider = Sdk.CreateLoggerProviderBuilder()
                .AddInMemoryExporter(exportedItems)
                .Build();

            var logger = provider.GetLogger(new InstrumentationScope());

            DateTime timestamp = DateTime.SpecifyKind(
                new DateTime(2022, 6, 30, 16, 0, 0),
                DateTimeKind.Unspecified);

            logger.EmitLog(new()
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
