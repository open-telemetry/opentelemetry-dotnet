// <copyright file="OtlpLogExporterTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpLogExporterTests : Http2UnencryptedSupportTests
    {
        [Fact]
        public void CheckToOtlpLogRecordStateValues()
        {
            List<LogRecord> logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.ParseStateValues = true;
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);
            Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);
            Assert.Contains("name", otlpLogRecord.Attributes.ToString());
            Assert.Contains("tomato", otlpLogRecord.Attributes.ToString());
            Assert.Contains("{OriginalFormat}", otlpLogRecord.Attributes.ToString());
        }

        [Fact]
        public void CheckToOtlpLogRecordEventId()
        {
            List<LogRecord> logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.ParseStateValues = true;
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation(new EventId(10, null), "Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);
            Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);

            var otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();

            // Event
            Assert.Contains("Id", otlpLogRecordAttributes);
            Assert.Contains("10", otlpLogRecordAttributes);

            logRecords.Clear();

            logger.LogInformation(new EventId(10, "MyEvent10"), "Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);

            logRecord = logRecords[0];
            otlpLogRecord = logRecord.ToOtlpLog();
            Assert.NotNull(otlpLogRecord);
            Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);

            otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();

            // Event
            Assert.Contains("Id", otlpLogRecordAttributes);
            Assert.Contains("10", otlpLogRecordAttributes);
            Assert.Contains("Name", otlpLogRecordAttributes);
            Assert.Contains("MyEvent10", otlpLogRecordAttributes);
        }

        [Fact]
        public void CheckToOtlpLogRecordTraceIdSpanIdFlagWithDroppedActivity()
        {
            List<LogRecord> logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Log within a dropped activity");
            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.Null(Activity.Current);
            Assert.True(otlpLogRecord.TraceId.IsEmpty);
            Assert.True(otlpLogRecord.SpanId.IsEmpty);
            Assert.True(otlpLogRecord.Flags == 0);
        }

        [Fact]
        public void CheckToOtlpLogRecordSpanIdTraceIdAndFlag()
        {
            // preparation
            var sampler = new AlwaysOnSampler();
            var exportedActivityList = new List<Activity>();
            var activitySourceName = "toOtlpLogRecordTest";
            var activitySource = new ActivitySource(activitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
                .SetSampler(sampler)
                .AddInMemoryExporter(exportedActivityList)
                .Build();

            using var activity = activitySource.StartActivity("Activity");

            // execution
            List<LogRecord> logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Log within activity marked as RecordOnly");

            // assertion
            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            var currentActivity = Activity.Current;
            Assert.NotNull(Activity.Current);

            var expectedTraceId = currentActivity.TraceId;
            var expectedSpanId = currentActivity.SpanId;
            Assert.Equal(expectedTraceId.ToString(), ActivityTraceId.CreateFromBytes(otlpLogRecord.TraceId.ToByteArray()).ToString());
            Assert.Equal(expectedSpanId.ToString(), ActivitySpanId.CreateFromBytes(otlpLogRecord.SpanId.ToByteArray()).ToString());
            Assert.Equal((uint)logRecord.TraceFlags, otlpLogRecord.Flags);
        }

        [Fact]
        public void CheckToOtlpLogRecordSeverityText()
        {
            List<LogRecord> logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                    options.IncludeFormattedMessage = true;
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);
            Assert.Equal(logRecord.LogLevel.ToString(), otlpLogRecord.SeverityText);
        }

        [Fact]
        public void CheckToOtlpLogRecordFormattedMessage()
        {
            List<LogRecord> logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                    options.IncludeFormattedMessage = true;
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("OpenTelemetry {Greeting} {Subject}!", "Hello", "World");
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);
            Assert.Equal(logRecord.FormattedMessage, otlpLogRecord.Body.StringValue);
        }

        [Fact]
        public void CheckToOtlpLogRecordExceptionAttributes()
        {
            List<LogRecord> logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var exceptionMessage = "Exception Message";
            var exception = new Exception(exceptionMessage);
            var message = "Exception Occurred";
            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation(exception, message);

            var logRecord = logRecords[0];
            var loggedException = logRecord.Exception;
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);
            var otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();
            Assert.Contains(SemanticConventions.AttributeExceptionType, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.GetType().Name, otlpLogRecordAttributes);

            Assert.Contains(SemanticConventions.AttributeExceptionMessage, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.Message, otlpLogRecordAttributes);

            Assert.Contains(SemanticConventions.AttributeExceptionStacktrace, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.ToInvariantString(), otlpLogRecordAttributes);
        }
    }
}
