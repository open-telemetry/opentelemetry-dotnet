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
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpLogExporterTests : Http2UnencryptedSupportTests
    {
        private static readonly SdkLimitOptions DefaultSdkLimitOptions = new();

        [Fact]
        public void AddOtlpLogExporterOptionsTest()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var loggerOptions = new OpenTelemetryLoggerOptions();
            Assert.False(loggerOptions.ParseStateValues);
            loggerOptions.AddOtlpExporter();
            Assert.True(loggerOptions.ParseStateValues);
        }

        [Fact]
        public void AddOtlpLogExporterSetsParseStateValueToTrue()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                    options.AddOtlpExporter();
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);
            var logRecord = logRecords[0];
            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.StateValues);
        }

        [Fact]
        public void AddOtlpLogExporterParseStateValueCanBeTurnedOff()
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                    options.AddOtlpExporter();
                    options.ParseStateValues = false;
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);
            var logRecord = logRecords[0];
            Assert.NotNull(logRecord.State);
            Assert.Null(logRecord.StateValues);
        }

        [Fact]
        public void AddOtlpLogExporterParseStateValueCanBeTurnedOffHosting()
        {
            var logRecords = new List<LogRecord>();

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureLogging(logging => logging.AddOpenTelemetry(options =>
            {
                options.AddInMemoryExporter(logRecords);
                options.AddOtlpExporter();
            }));

            hostBuilder.ConfigureServices(services =>
            services.Configure<OpenTelemetryLoggerOptions>(options => options.ParseStateValues = false));

            var host = hostBuilder.Build();
            var loggerFactory = host.Services.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);
            var logRecord = logRecords[0];
            Assert.NotNull(logRecord.State);
            Assert.Null(logRecord.StateValues);
        }

        [Fact]
        public void OtlpLogRecordTestWhenStateValuesArePopulated()
        {
            var logRecords = new List<LogRecord>();
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
            var otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

            Assert.NotNull(otlpLogRecord);
            Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);
            Assert.Equal(4, otlpLogRecord.Attributes.Count);

            var attribute = otlpLogRecord.Attributes[0];
            Assert.Equal("dotnet.ilogger.category", attribute.Key);
            Assert.Equal("OtlpLogExporterTests", attribute.Value.StringValue);

            attribute = otlpLogRecord.Attributes[1];
            Assert.Equal("name", attribute.Key);
            Assert.Equal("tomato", attribute.Value.StringValue);

            attribute = otlpLogRecord.Attributes[2];
            Assert.Equal("price", attribute.Key);
            Assert.Equal(2.99, attribute.Value.DoubleValue);

            attribute = otlpLogRecord.Attributes[3];
            Assert.Equal("{OriginalFormat}", attribute.Key);
            Assert.Equal("Hello from {name} {price}.", attribute.Value.StringValue);
        }

        [Fact]
        public void CheckToOtlpLogRecordLoggerCategory()
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger1 = loggerFactory.CreateLogger("CategoryA");
            logger1.LogInformation("Hello");
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);
            Assert.NotNull(otlpLogRecord);
            Assert.Single(otlpLogRecord.Attributes);

            var attribute = otlpLogRecord.Attributes[0];
            Assert.Equal("dotnet.ilogger.category", attribute.Key);
            Assert.Equal("CategoryA", attribute.Value.StringValue);

            logRecords.Clear();
            var logger2 = loggerFactory.CreateLogger(string.Empty);
            logger2.LogInformation("Hello");
            Assert.Single(logRecords);

            logRecord = logRecords[0];
            otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);
            Assert.NotNull(otlpLogRecord);
            Assert.Empty(otlpLogRecord.Attributes);
        }

        [Fact]
        public void CheckToOtlpLogRecordEventId()
        {
            var logRecords = new List<LogRecord>();
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
            var otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

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
            otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);
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
        public void CheckToOtlpLogRecordTraceIdSpanIdFlagWithNoActivity()
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Log when there is no activity.");
            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

            Assert.Null(Activity.Current);
            Assert.True(otlpLogRecord.TraceId.IsEmpty);
            Assert.True(otlpLogRecord.SpanId.IsEmpty);
            Assert.True(otlpLogRecord.Flags == 0);
        }

        [Fact]
        public void CheckToOtlpLogRecordSpanIdTraceIdAndFlag()
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");

            ActivityTraceId expectedTraceId = default;
            ActivitySpanId expectedSpanId = default;
            using (var activity = new Activity(Utils.GetCurrentMethodName()).Start())
            {
                logger.LogInformation("Log within an activity.");

                expectedTraceId = activity.TraceId;
                expectedSpanId = activity.SpanId;
            }

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

            Assert.Equal(expectedTraceId.ToString(), ActivityTraceId.CreateFromBytes(otlpLogRecord.TraceId.ToByteArray()).ToString());
            Assert.Equal(expectedSpanId.ToString(), ActivitySpanId.CreateFromBytes(otlpLogRecord.SpanId.ToByteArray()).ToString());
            Assert.Equal((uint)logRecord.TraceFlags, otlpLogRecord.Flags);
        }

        [Theory]
        [InlineData(LogLevel.Trace)]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Information)]
        [InlineData(LogLevel.Warning)]
        [InlineData(LogLevel.Error)]
        [InlineData(LogLevel.Critical)]
        public void CheckToOtlpLogRecordSeverityLevelAndText(LogLevel logLevel)
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                    options.IncludeFormattedMessage = true;
                })
                .AddFilter("CheckToOtlpLogRecordSeverityLevelAndText", LogLevel.Trace);
            });

            var logger = loggerFactory.CreateLogger("CheckToOtlpLogRecordSeverityLevelAndText");
            logger.Log(logLevel, "Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

            Assert.NotNull(otlpLogRecord);
            Assert.Equal(logRecord.LogLevel.ToString(), otlpLogRecord.SeverityText);
            switch (logLevel)
            {
                case LogLevel.Trace:
                    Assert.Equal(OtlpLogs.SeverityNumber.Trace, otlpLogRecord.SeverityNumber);
                    break;
                case LogLevel.Debug:
                    Assert.Equal(OtlpLogs.SeverityNumber.Debug, otlpLogRecord.SeverityNumber);
                    break;
                case LogLevel.Information:
                    Assert.Equal(OtlpLogs.SeverityNumber.Info, otlpLogRecord.SeverityNumber);
                    break;
                case LogLevel.Warning:
                    Assert.Equal(OtlpLogs.SeverityNumber.Warn, otlpLogRecord.SeverityNumber);
                    break;
                case LogLevel.Error:
                    Assert.Equal(OtlpLogs.SeverityNumber.Error, otlpLogRecord.SeverityNumber);
                    break;
                case LogLevel.Critical:
                    Assert.Equal(OtlpLogs.SeverityNumber.Fatal, otlpLogRecord.SeverityNumber);
                    break;
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckToOtlpLogRecordBodyIsPopulated(bool includeFormattedMessage)
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                    options.IncludeFormattedMessage = includeFormattedMessage;
                    options.ParseStateValues = true;
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");

            // Scenario 1 - Using ExtensionMethods on ILogger.Log
            logger.LogInformation("OpenTelemetry {Greeting} {Subject}!", "Hello", "World");
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

            Assert.NotNull(otlpLogRecord);
            if (includeFormattedMessage)
            {
                Assert.Equal(logRecord.FormattedMessage, otlpLogRecord.Body.StringValue);
            }
            else
            {
                Assert.Equal("OpenTelemetry {Greeting} {Subject}!", otlpLogRecord.Body.StringValue);
            }

            logRecords.Clear();

            // Scenario 2 - Using the raw ILogger.Log Method
            logger.Log(LogLevel.Information, default, "state", exception: null, (st, ex) => "Formatted Message");
            Assert.Single(logRecords);

            logRecord = logRecords[0];
            otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

            Assert.NotNull(otlpLogRecord);
            if (includeFormattedMessage)
            {
                Assert.Equal(logRecord.FormattedMessage, otlpLogRecord.Body.StringValue);
            }
            else
            {
                Assert.Null(otlpLogRecord.Body);
            }

            logRecords.Clear();

            // Scenario 3 - Using the raw ILogger.Log Method, but with null
            // formatter.
            logger.Log(LogLevel.Information, default, "state", exception: null, formatter: null);
            Assert.Single(logRecords);

            logRecord = logRecords[0];
            otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

            Assert.NotNull(otlpLogRecord);

            // There is no formatter, so no way to populate Body.
            // Exporter won't even attempt to do ToString() on State.
            if (includeFormattedMessage)
            {
                Assert.Null(otlpLogRecord.Body);
            }
            else
            {
                Assert.Null(otlpLogRecord.Body);
            }
        }

        [Fact]
        public void CheckToOtlpLogRecordExceptionAttributes()
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation(new Exception("Exception Message"), "Exception Occurred");

            var logRecord = logRecords[0];
            var loggedException = logRecord.Exception;
            var otlpLogRecord = logRecord.ToOtlpLog(DefaultSdkLimitOptions);

            Assert.NotNull(otlpLogRecord);
            var otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();
            Assert.Contains(SemanticConventions.AttributeExceptionType, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.GetType().Name, otlpLogRecordAttributes);

            Assert.Contains(SemanticConventions.AttributeExceptionMessage, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.Message, otlpLogRecordAttributes);

            Assert.Contains(SemanticConventions.AttributeExceptionStacktrace, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.ToInvariantString(), otlpLogRecordAttributes);
        }

        [Fact]
        public void CheckToOtlpLogRecordRespectsAttributeLimits()
        {
            var sdkLimitOptions = new SdkLimitOptions
            {
                AttributeCountLimit = 3, // 3 => LogCategory, exception.type and exception.message
                AttributeValueLengthLimit = 8,
            };

            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddInMemoryExporter(logRecords);
                });
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation(new NotSupportedException("I'm the exception message."), "Exception Occurred");

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog(sdkLimitOptions);

            Assert.NotNull(otlpLogRecord);
            Assert.Equal(1u, otlpLogRecord.DroppedAttributesCount);

            var exceptionTypeAtt = TryGetAttribute(otlpLogRecord, SemanticConventions.AttributeExceptionType);
            Assert.NotNull(exceptionTypeAtt);

            // "NotSuppo" == first 8 chars from the exception typename "NotSupportedException"
            Assert.Equal("NotSuppo", exceptionTypeAtt.Value.StringValue);
            var exceptionMessageAtt = TryGetAttribute(otlpLogRecord, SemanticConventions.AttributeExceptionMessage);
            Assert.NotNull(exceptionMessageAtt);

            // "I'm the " == first 8 chars from the exception message
            Assert.Equal("I'm the ", exceptionMessageAtt.Value.StringValue);

            var exceptionStackTraceAtt = TryGetAttribute(otlpLogRecord, SemanticConventions.AttributeExceptionStacktrace);
            Assert.Null(exceptionStackTraceAtt);
        }

        private static OtlpCommon.KeyValue TryGetAttribute(OtlpLogs.LogRecord record, string key)
        {
            return record.Attributes.FirstOrDefault(att => att.Key == key);
        }
    }
}
