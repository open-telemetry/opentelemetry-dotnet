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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpLogExporterTests : Http2UnencryptedSupportTests
    {
        [Fact]
        public void AddOtlpLogExporterReceivesAttributesWithParseStateValueSetToFalse()
        {
            bool optionsValidated = false;

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddOpenTelemetry()
                    .ConfigureServices(services => services.Configure<OpenTelemetryLoggerOptions>(o =>
                    {
                        optionsValidated = true;
                        Assert.True(o.IncludeState);
                        Assert.False(o.ParseStateValues);
                    }))
                    .AddInMemoryExporter(logRecords)
                    .AddOtlpExporter();
            });

            Assert.True(optionsValidated);

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);
            var logRecord = logRecords[0];
#pragma warning disable CS0618 // Type or member is obsolete
            Assert.Null(logRecord.State);
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.NotNull(logRecord.Attributes);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddOtlpLogExporterParseStateValueCanBeTurnedOff(bool parseState)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddOpenTelemetry(options => options.ParseStateValues = parseState)
                    .AddInMemoryExporter(logRecords)
                    .AddOtlpExporter();
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.Log(LogLevel.Information, default, new { propertyA = "valueA" }, null, (s, e) => "Custom state log message");
            Assert.Single(logRecords);

            var logRecord = logRecords[0];

#pragma warning disable CS0618 // Type or member is obsolete
            if (parseState)
            {
                Assert.Null(logRecord.State);
                Assert.NotNull(logRecord.Attributes);
                Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "propertyA" && (string)kvp.Value == "valueA");
            }
            else
            {
                Assert.NotNull(logRecord.State);
                Assert.Null(logRecord.Attributes);
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddOtlpLogExporterParseStateValueCanBeTurnedOffHosting(bool parseState)
        {
            var logRecords = new List<LogRecord>();

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureLogging(logging => logging
                .AddOpenTelemetry()
                .AddInMemoryExporter(logRecords)
                .AddOtlpExporter());

            hostBuilder.ConfigureServices(services =>
            services.Configure<OpenTelemetryLoggerOptions>(options => options.ParseStateValues = parseState));

            var host = hostBuilder.Build();
            var loggerFactory = host.Services.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.Log(LogLevel.Information, default, new { propertyA = "valueA" }, null, (s, e) => "Custom state log message");
            Assert.Single(logRecords);

            var logRecord = logRecords[0];

#pragma warning disable CS0618 // Type or member is obsolete
            if (parseState)
            {
                Assert.Null(logRecord.State);
                Assert.NotNull(logRecord.Attributes);
                Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "propertyA" && (string)kvp.Value == "valueA");
            }
            else
            {
                Assert.NotNull(logRecord.State);
                Assert.Null(logRecord.Attributes);
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Fact]
        public void AddOtlpLogExporterNamedOptionsSupported()
        {
            int defaultExporterOptionsConfigureOptionsInvocations = 0;
            int namedExporterOptionsConfigureOptionsInvocations = 0;

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddOpenTelemetry()
                    .ConfigureServices(services =>
                    {
                        services.Configure<OtlpExporterOptions>(o => defaultExporterOptionsConfigureOptionsInvocations++);

                        services.Configure<OtlpExporterOptions>("Exporter2", o => namedExporterOptionsConfigureOptionsInvocations++);
                    })
                    .AddOtlpExporter()
                    .AddOtlpExporter("Exporter2", o => { });
            });

            Assert.Equal(1, defaultExporterOptionsConfigureOptionsInvocations);
            Assert.Equal(1, namedExporterOptionsConfigureOptionsInvocations);
        }

        [Fact]
        public void OtlpLogRecordTestWhenStateValuesArePopulated()
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddOpenTelemetry(options =>
                    {
                        options.IncludeFormattedMessage = true;
                        options.ParseStateValues = true;
                    })
                    .AddInMemoryExporter(logRecords);
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);

            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

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
                builder.AddOpenTelemetry().AddInMemoryExporter(logRecords);
            });

            var logger1 = loggerFactory.CreateLogger("CategoryA");
            logger1.LogInformation("Hello");
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();
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
            otlpLogRecord = logRecord.ToOtlpLog();
            Assert.NotNull(otlpLogRecord);
            Assert.Empty(otlpLogRecord.Attributes);
        }

        [Fact]
        public void CheckToOtlpLogRecordEventId()
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddOpenTelemetry(options =>
                    {
                        options.IncludeFormattedMessage = true;
                        options.ParseStateValues = true;
                    })
                    .AddInMemoryExporter(logRecords);
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
        public void CheckToOtlpLogRecordTraceIdSpanIdFlagWithNoActivity()
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry().AddInMemoryExporter(logRecords);
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation("Log when there is no activity.");
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
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry().AddInMemoryExporter(logRecords);
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
            var otlpLogRecord = logRecord.ToOtlpLog();

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
                builder
                    .AddFilter("CheckToOtlpLogRecordSeverityLevelAndText", LogLevel.Trace)
                    .AddOpenTelemetry(options => options.IncludeFormattedMessage = true).AddInMemoryExporter(logRecords);
            });

            var logger = loggerFactory.CreateLogger("CheckToOtlpLogRecordSeverityLevelAndText");
            logger.Log(logLevel, "Hello from {name} {price}.", "tomato", 2.99);
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);
            Assert.Equal(logRecord.Severity.ToString(), otlpLogRecord.SeverityText);
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
                builder
                    .AddOpenTelemetry(options =>
                    {
                        options.IncludeFormattedMessage = includeFormattedMessage;
                        options.ParseStateValues = true;
                    })
                    .AddInMemoryExporter(logRecords);
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");

            // Scenario 1 - Using ExtensionMethods on ILogger.Log
            logger.LogInformation("OpenTelemetry {Greeting} {Subject}!", "Hello", "World");
            Assert.Single(logRecords);

            var logRecord = logRecords[0];
            var otlpLogRecord = logRecord.ToOtlpLog();

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
            otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);

            // Formatter is always called if no template can be found.
            Assert.Equal(logRecord.FormattedMessage, otlpLogRecord.Body.StringValue);
            Assert.Equal(logRecord.Body, otlpLogRecord.Body.StringValue);

            logRecords.Clear();

            // Scenario 3 - Using the raw ILogger.Log Method, but with null
            // formatter.
            logger.Log(LogLevel.Information, default, "state", exception: null, formatter: null);
            Assert.Single(logRecords);

            logRecord = logRecords[0];
            otlpLogRecord = logRecord.ToOtlpLog();

            Assert.NotNull(otlpLogRecord);

            // There is no formatter, so no way to populate Body.
            // Exporter won't even attempt to do ToString() on State.
            Assert.Null(otlpLogRecord.Body);
        }

        [Fact]
        public void CheckToOtlpLogRecordExceptionAttributes()
        {
            var logRecords = new List<LogRecord>();
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry().AddInMemoryExporter(logRecords);
            });

            var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
            logger.LogInformation(new Exception("Exception Message"), "Exception Occurred");

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

        [Fact]
        public void CheckAddBatchInstrumentationScopeProcessed()
        {
            List<LogRecord> exportedLogRecords = new();

            using (var provider = Sdk.CreateLoggerProviderBuilder()
                .AddInMemoryExporter(exportedLogRecords)
                .Build())
            {
                var loggerA = provider.GetLogger(new InstrumentationScope("testLogger1")
                {
                    Attributes = new Dictionary<string, object> { ["mycustom.key1"] = "value1" },
                });
                var loggerB = provider.GetLogger(
                    new LoggerOptions(
                        new InstrumentationScope("testLogger2")
                        {
                            Attributes = new Dictionary<string, object> { ["mycustom.key2"] = "value2" },
                        })
                    {
                        EventDomain = "testLogger2EventDomain",
                    });

                loggerA.EmitLog(default, default);
                loggerB.EmitEvent("event1", default, default);
            }

            Assert.Equal(2, exportedLogRecords.Count);

            var batch = new Batch<LogRecord>(exportedLogRecords.ToArray(), 2);

            ExportLogsServiceRequest request = new();

            request.AddBatch(new(), in batch);

            Assert.Equal(2, request.ResourceLogs[0].ScopeLogs.Count);

            Assert.Equal("testLogger1", request.ResourceLogs[0].ScopeLogs[0].Scope.Name);
            Assert.Equal("mycustom.key1", request.ResourceLogs[0].ScopeLogs[0].Scope.Attributes[0].Key);
            Assert.Equal("value1", request.ResourceLogs[0].ScopeLogs[0].Scope.Attributes[0].Value.StringValue);

            Assert.Equal("testLogger2", request.ResourceLogs[0].ScopeLogs[1].Scope.Name);
            Assert.Equal("mycustom.key2", request.ResourceLogs[0].ScopeLogs[1].Scope.Attributes[0].Key);
            Assert.Equal("value2", request.ResourceLogs[0].ScopeLogs[1].Scope.Attributes[0].Value.StringValue);
            Assert.Equal("event.domain", request.ResourceLogs[0].ScopeLogs[1].Scope.Attributes[1].Key);
            Assert.Equal("testLogger2EventDomain", request.ResourceLogs[0].ScopeLogs[1].Scope.Attributes[1].Value.StringValue);
        }
    }
}
