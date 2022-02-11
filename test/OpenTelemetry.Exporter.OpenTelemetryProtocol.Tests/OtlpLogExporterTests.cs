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
        private readonly ILogger logger;
        private readonly List<LogRecord> exportedItems = new();
        private readonly ILoggerFactory loggerFactory;
        private OpenTelemetryLoggerOptions options;

        public OtlpLogExporterTests()
        {
            this.loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    this.options = options;
                    this.options.AddInMemoryExporter(this.exportedItems);
                });
            });

            this.logger = this.loggerFactory.CreateLogger<OtlpLogExporterTests>();
        }

        [Fact]
        public void CheckToOtlpLogRecordStateValues()
        {
            this.options.IncludeFormattedMessage = true;
            this.options.ParseStateValues = true;

            this.logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(this.exportedItems);

            var logRecord = this.exportedItems[0];
            var otlpLogRecord = logRecord.ToOtlpLog();
            Assert.NotNull(otlpLogRecord);
            Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);
        }

        [Fact]
        public void CheckToOtlpLogRecordEventId()
        {
            this.options.IncludeFormattedMessage = true;
            this.options.ParseStateValues = true;

            this.logger.LogInformation(new EventId(10, null), "Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(this.exportedItems);

            var logRecord = this.exportedItems[0];
            var otlpLogRecord = logRecord.ToOtlpLog();
            Assert.NotNull(otlpLogRecord);
            Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);
            this.exportedItems.Clear();

            this.logger.LogInformation(new EventId(10, "MyEvent10"), "Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(this.exportedItems);

            logRecord = this.exportedItems[0];
            otlpLogRecord = logRecord.ToOtlpLog();
            Assert.NotNull(otlpLogRecord);
            Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);
        }

        [Fact]
        public void CheckToOtlpLogRecordTraceIdSpanIdFlagWithDroppedActivity()
        {
            this.logger.LogInformation("Log within a dropped activity");
            var logRecord = this.exportedItems[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.Null(Activity.Current);
            Assert.True(otlpLogRecord.TraceId.IsEmpty);
            Assert.True(otlpLogRecord.SpanId.IsEmpty);
            Assert.True(otlpLogRecord.Flags == 0);
        }

        [Fact]
        public void CheckToOtlpLogRecordSpanIdTraceIdFlag()
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
            this.logger.LogInformation("Log within activity marked as RecordOnly");

            // assertion
            var logRecord = this.exportedItems[0];
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
            this.options.IncludeFormattedMessage = true;

            this.logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(this.exportedItems);

            var logRecord = this.exportedItems[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);
            Assert.Equal(logRecord.LogLevel.ToString(), otlpLogRecord.SeverityText);
        }

        [Fact]
        public void CheckToOtlpLogRecordFormattedMessage()
        {
            this.options.IncludeFormattedMessage = true;

            this.logger.LogInformation("OpenTelemetry!");
            Assert.Single(this.exportedItems);

            var logRecord = this.exportedItems[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);
            Assert.Equal(logRecord.FormattedMessage, otlpLogRecord.Body.StringValue);
        }

        [Fact]
        public void CheckToOtlpLogRecordExceptionAttributes()
        {
            var exceptionMessage = "Exception Message";
            var exception = new Exception(exceptionMessage);
            var message = "Exception Occurred";
            this.logger.LogInformation(exception, message);

            var logRecord = this.exportedItems[0];
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
