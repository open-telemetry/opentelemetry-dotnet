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

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using static OpenTelemetry.Proto.Common.V1.AnyValue;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpLogExporterTests : Http2UnencryptedSupportTests
{
    private static readonly SdkLimitOptions DefaultSdkLimitOptions = new();

    [Fact]
    public void AddOtlpLogExporterReceivesAttributesWithParseStateValueSetToFalse()
    {
        bool optionsValidated = false;

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddOpenTelemetry(options => options
                    .AddInMemoryExporter(logRecords)
                    .AddOtlpExporter());

            builder.Services.Configure<OpenTelemetryLoggerOptions>(o =>
            {
                optionsValidated = true;
                Assert.False(o.ParseStateValues);
            });
        });

        Assert.True(optionsValidated);

        var logger = loggerFactory.CreateLogger("OtlpLogExporterTests");
        logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
        Assert.Single(logRecords);
        var logRecord = logRecords[0];
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.NotNull(logRecord.State);
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
                .AddOpenTelemetry(options =>
                {
                    options.ParseStateValues = parseState;
                    options
                        .AddInMemoryExporter(logRecords)
                        .AddOtlpExporter();
                });
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

            // Note: We currently do not support parsing custom states which do
            // not implement the standard interfaces. We return empty attributes
            // for these.
            Assert.Empty(logRecord.Attributes);
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
            .AddOpenTelemetry(options => options
                .AddInMemoryExporter(logRecords)
                .AddOtlpExporter()));

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

            // Note: We currently do not support parsing custom states which do
            // not implement the standard interfaces. We return empty attributes
            // for these.
            Assert.Empty(logRecord.Attributes);
        }
        else
        {
            Assert.NotNull(logRecord.State);
            Assert.Null(logRecord.Attributes);
        }
#pragma warning restore CS0618 // Type or member is obsolete
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

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);
        Assert.Equal(3, otlpLogRecord.Attributes.Count);
        var index = 0;
        var attribute = otlpLogRecord.Attributes[index];

        /*
        Assert.Equal("dotnet.ilogger.category", attribute.Key);
        Assert.Equal("OtlpLogExporterTests", attribute.Value.StringValue);
        attribute = otlpLogRecord.Attributes[++index];
        */

        Assert.Equal("name", attribute.Key);
        Assert.Equal("tomato", attribute.Value.StringValue);

        attribute = otlpLogRecord.Attributes[++index];
        Assert.Equal("price", attribute.Key);
        Assert.Equal(2.99, attribute.Value.DoubleValue);

        attribute = otlpLogRecord.Attributes[++index];
        Assert.Equal("{OriginalFormat}", attribute.Key);
        Assert.Equal("Hello from {name} {price}.", attribute.Value.StringValue);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(null)]
    public void CheckToOtlpLogRecordLoggerCategory(string emitLogEventAndCategoryAttributes)
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

        var configuration = new ConfigurationBuilder()
           .AddInMemoryCollection(new Dictionary<string, string> { [ExperimentalOptions.EMITCATEGORYANDEVENTATTRIBUTES] = emitLogEventAndCategoryAttributes })
           .Build();

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new(configuration));

        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

        if (emitLogEventAndCategoryAttributes == "true")
        {
            Assert.NotNull(otlpLogRecord);
            Assert.Single(otlpLogRecord.Attributes);

            var attribute = otlpLogRecord.Attributes[0];
            Assert.Equal("dotnet.ilogger.category", attribute.Key);
            Assert.Equal("CategoryA", attribute.Value.StringValue);
        }
        else
        {
            Assert.NotNull(otlpLogRecord);
            Assert.Empty(otlpLogRecord.Attributes);
        }

        logRecords.Clear();
        var logger2 = loggerFactory.CreateLogger(string.Empty);
        logger2.LogInformation("Hello");
        Assert.Single(logRecords);

        logRecord = logRecords[0];
        otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.NotNull(otlpLogRecord);
        Assert.Empty(otlpLogRecord.Attributes);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(null)]
    public void CheckToOtlpLogRecordEventId(string emitLogEventAndCategoryAttributes)
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

        var configuration = new ConfigurationBuilder()
          .AddInMemoryCollection(new Dictionary<string, string> { [ExperimentalOptions.EMITCATEGORYANDEVENTATTRIBUTES] = emitLogEventAndCategoryAttributes })
          .Build();

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new(configuration));

        var logRecord = logRecords[0];

        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);

        // Event
        var otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();
        if (emitLogEventAndCategoryAttributes == "true")
        {
            Assert.Contains("event.id", otlpLogRecordAttributes);
            Assert.Contains("10", otlpLogRecordAttributes);
        }
        else
        {
            Assert.DoesNotContain("event.id", otlpLogRecordAttributes);
        }

        logRecords.Clear();

        logger.LogInformation(new EventId(10, "MyEvent10"), "Hello from {name} {price}.", "tomato", 2.99);
        Assert.Single(logRecords);

        logRecord = logRecords[0];
        otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.NotNull(otlpLogRecord);
        Assert.Equal("Hello from tomato 2.99.", otlpLogRecord.Body.StringValue);

        // Event
        otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();
        if (emitLogEventAndCategoryAttributes == "true")
        {
            Assert.Contains("event.id", otlpLogRecordAttributes);
            Assert.Contains("10", otlpLogRecordAttributes);
            Assert.Contains("event.name", otlpLogRecordAttributes);
            Assert.Contains("MyEvent10", otlpLogRecordAttributes);
        }
        else
        {
            Assert.DoesNotContain("event.id", otlpLogRecordAttributes);
            Assert.DoesNotContain("event.name", otlpLogRecordAttributes);
        }
    }

    [Fact]
    public void CheckToOtlpLogRecordTimestamps()
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
        logger.LogInformation("Log message");
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.True(otlpLogRecord.TimeUnixNano > 0);
        Assert.True(otlpLogRecord.ObservedTimeUnixNano > 0);
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

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

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

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

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

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.Equal(logRecord.LogLevel.ToString(), otlpLogRecord.SeverityText);
#pragma warning restore CS0618 // Type or member is obsolete
        Assert.Equal((int)logRecord.Severity, (int)otlpLogRecord.SeverityNumber);
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

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());

        var logRecord = logRecords[0];
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

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
        otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

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
        otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);

        // There is no formatter, we call ToString on state
        Assert.Equal("state", otlpLogRecord.Body.StringValue);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void CheckToOtlpLogRecordExceptionAttributes(string emitExceptionAttributes)
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [ExperimentalOptions.EMITLOGEXCEPTIONATTRIBUTES] = emitExceptionAttributes })
            .Build();

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new(configuration));

        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        var otlpLogRecordAttributes = otlpLogRecord.Attributes.ToString();

        if (emitExceptionAttributes == "true")
        {
            Assert.Contains(SemanticConventions.AttributeExceptionType, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.GetType().Name, otlpLogRecordAttributes);

            Assert.Contains(SemanticConventions.AttributeExceptionMessage, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.Message, otlpLogRecordAttributes);

            Assert.Contains(SemanticConventions.AttributeExceptionStacktrace, otlpLogRecordAttributes);
            Assert.Contains(logRecord.Exception.ToInvariantString(), otlpLogRecordAttributes);
        }
        else
        {
            Assert.DoesNotContain(SemanticConventions.AttributeExceptionType, otlpLogRecordAttributes);
            Assert.DoesNotContain(logRecord.Exception.GetType().Name, otlpLogRecordAttributes);

            Assert.DoesNotContain(SemanticConventions.AttributeExceptionMessage, otlpLogRecordAttributes);
            Assert.DoesNotContain(logRecord.Exception.Message, otlpLogRecordAttributes);

            Assert.DoesNotContain(SemanticConventions.AttributeExceptionStacktrace, otlpLogRecordAttributes);
            Assert.DoesNotContain(logRecord.Exception.ToInvariantString(), otlpLogRecordAttributes);
        }
    }

    [Fact]
    public void CheckToOtlpLogRecordRespectsAttributeLimits()
    {
        var sdkLimitOptions = new SdkLimitOptions
        {
            AttributeCountLimit = 2,
            AttributeValueLengthLimit = 8,
        };

        var logRecords = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.ParseStateValues = true;
                options.AddInMemoryExporter(logRecords);
            });
        });

        var logger = loggerFactory.CreateLogger(string.Empty);
        logger.LogInformation("OpenTelemetry {AttributeOne} {AttributeTwo} {AttributeThree}!", "I'm an attribute", "I too am an attribute", "I get dropped :(");

        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(sdkLimitOptions, new());

        var logRecord = logRecords[0];
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);

        Assert.NotNull(otlpLogRecord);
        Assert.Equal(1u, otlpLogRecord.DroppedAttributesCount);

        var attribute = TryGetAttribute(otlpLogRecord, "AttributeOne");
        Assert.NotNull(attribute);

        // "I'm an a" == first 8 chars from the first attribute "I'm an attribute"
        Assert.Equal("I'm an a", attribute.Value.StringValue);
        attribute = TryGetAttribute(otlpLogRecord, "AttributeTwo");
        Assert.NotNull(attribute);

        // "I too am" == first 8 chars from the second attribute "I too am an attribute"
        Assert.Equal("I too am", attribute.Value.StringValue);

        attribute = TryGetAttribute(otlpLogRecord, "AttributeThree");
        Assert.Null(attribute);
    }

    [Fact]
    public void Export_WhenExportClientIsProvidedInCtor_UsesProvidedExportClient()
    {
        // Arrange.
        var fakeExportClient = new Mock<IExportClient<OtlpCollector.ExportLogsServiceRequest>>();
        var emptyLogRecords = Array.Empty<LogRecord>();
        var emptyBatch = new Batch<LogRecord>(emptyLogRecords, emptyLogRecords.Length);
        var sut = new OtlpLogExporter(
                        new OtlpExporterOptions(),
                        new SdkLimitOptions(),
                        fakeExportClient.Object);

        // Act.
        var result = sut.Export(emptyBatch);

        // Assert.
        fakeExportClient.Verify(x => x.SendExportRequest(It.IsAny<OtlpCollector.ExportLogsServiceRequest>(), default), Times.Once());
    }

    [Fact]
    public void Export_WhenExportClientThrowsException_ReturnsExportResultFailure()
    {
        // Arrange.
        var fakeExportClient = new Mock<IExportClient<OtlpCollector.ExportLogsServiceRequest>>();
        var emptyLogRecords = Array.Empty<LogRecord>();
        var emptyBatch = new Batch<LogRecord>(emptyLogRecords, emptyLogRecords.Length);
        fakeExportClient
            .Setup(_ => _.SendExportRequest(It.IsAny<OtlpCollector.ExportLogsServiceRequest>(), default))
            .Throws(new Exception("Test Exception"));
        var sut = new OtlpLogExporter(
                        new OtlpExporterOptions(),
                        new SdkLimitOptions(),
                        fakeExportClient.Object);

        // Act.
        var result = sut.Export(emptyBatch);

        // Assert.
        Assert.Equal(ExportResult.Failure, result);
    }

    [Fact]
    public void Export_WhenExportIsSuccessful_ReturnsExportResultSuccess()
    {
        // Arrange.
        var fakeExportClient = new Mock<IExportClient<OtlpCollector.ExportLogsServiceRequest>>();
        var emptyLogRecords = Array.Empty<LogRecord>();
        var emptyBatch = new Batch<LogRecord>(emptyLogRecords, emptyLogRecords.Length);
        fakeExportClient
            .Setup(_ => _.SendExportRequest(It.IsAny<OtlpCollector.ExportLogsServiceRequest>(), default))
            .Returns(true);
        var sut = new OtlpLogExporter(
                        new OtlpExporterOptions(),
                        new SdkLimitOptions(),
                        fakeExportClient.Object);

        // Act.
        var result = sut.Export(emptyBatch);

        // Assert.
        Assert.Equal(ExportResult.Success, result);
    }

    [Fact]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsFalse_DoesNotContainScopeAttribute()
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = false;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger("Some category");

        const string expectedScopeKey = "Some scope key";
        const string expectedScopeValue = "Some scope value";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(expectedScopeKey, expectedScopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        var actualScope = TryGetAttribute(otlpLogRecord, expectedScopeKey);
        Assert.Null(actualScope);
    }

    [Theory]
    [InlineData("Some scope value")]
    [InlineData('a')]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeStringValue(object scopeValue)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(ValueOneofCase.StringValue, actualScope.Value.ValueCase);
        Assert.Equal(scopeValue.ToString(), actualScope.Value.StringValue);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeBoolValue(bool scopeValue)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(ValueOneofCase.BoolValue, actualScope.Value.ValueCase);
        Assert.Equal(scopeValue.ToString(), actualScope.Value.BoolValue.ToString());
    }

    [Theory]
    [InlineData(byte.MinValue)]
    [InlineData(byte.MaxValue)]
    [InlineData(sbyte.MinValue)]
    [InlineData(sbyte.MaxValue)]
    [InlineData(short.MinValue)]
    [InlineData(short.MaxValue)]
    [InlineData(ushort.MinValue)]
    [InlineData(ushort.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    [InlineData(uint.MinValue)]
    [InlineData(uint.MaxValue)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeIntValue(object scopeValue)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(ValueOneofCase.IntValue, actualScope.Value.ValueCase);
        Assert.Equal(scopeValue.ToString(), actualScope.Value.IntValue.ToString());
    }

    [Theory]
    [InlineData(float.MinValue)]
    [InlineData(float.MaxValue)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeDoubleValueForFloat(float scopeValue)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(ValueOneofCase.DoubleValue, actualScope.Value.ValueCase);
        Assert.Equal(((double)scopeValue).ToString(), actualScope.Value.DoubleValue.ToString());
    }

    [Theory]
    [InlineData(double.MinValue)]
    [InlineData(double.MaxValue)]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_ContainsScopeAttributeDoubleValueForDouble(double scopeValue)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey, scopeValue),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(scopeValue.ToString(), actualScope.Value.DoubleValue.ToString());
    }

    [Fact]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeStateIsOfTypeString_ScopeIsIgnored()
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeState = "Some scope state";

        // Act.
        using (logger.BeginScope(scopeState))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.NotNull(otlpLogRecord);
        Assert.Empty(otlpLogRecord.Attributes);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(float))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(char))]
    [InlineData(typeof(bool))]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeStateIsOfPrimitiveTypes_ScopeIsIgnored(Type typeOfScopeState)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        var scopeState = Activator.CreateInstance(typeOfScopeState);

        // Act.
        using (logger.BeginScope(scopeState))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.NotNull(otlpLogRecord);
        Assert.Empty(otlpLogRecord.Attributes);
    }

    [Fact]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeStateIsOfDictionaryType_ScopeIsProcessed()
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";
        const string scopeValue = "Some scope value";
        var scopeState = new Dictionary<string, object>() { { scopeKey, scopeValue } };

        // Act.
        using (logger.BeginScope(scopeState))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(scopeValue, actualScope.Value.StringValue);
    }

    [Theory]
    [InlineData(typeof(List<KeyValuePair<string, object>>))]
    [InlineData(typeof(ReadOnlyCollection<KeyValuePair<string, object>>))]
    [InlineData(typeof(HashSet<KeyValuePair<string, object>>))]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeStateIsOfEnumerableType_ScopeIsProcessed(Type typeOfScopeState)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeKey = "Some scope key";
        const string scopeValue = "Some scope value";
        var scopeValues = new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>(scopeKey, scopeValue) };
        var scopeState = Activator.CreateInstance(typeOfScopeState, scopeValues) as ICollection<KeyValuePair<string, object>>;

        // Act.
        using (logger.BeginScope(scopeState))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        Assert.Single(otlpLogRecord.Attributes);
        var actualScope = TryGetAttribute(otlpLogRecord, scopeKey);
        Assert.NotNull(actualScope);
        Assert.Equal(scopeKey, actualScope.Key);
        Assert.Equal(scopeValue, actualScope.Value.StringValue);
    }

    [Theory]
    [InlineData("Same scope key", "Same scope key")]
    [InlineData("Scope key 1", "Scope key 2")]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndMultipleScopesAreAdded_ContainsAllAddedScopeValues(string scopeKey1, string scopeKey2)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeValue1 = "Some scope value";
        const string scopeValue2 = "Some other scope value";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey1, scopeValue1),
            new KeyValuePair<string, object>(scopeKey2, scopeValue2),
        }))
        {
            logger.LogInformation("Some log information message.");
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        var allScopeValues = otlpLogRecord.Attributes
            .Where(_ => _.Key == scopeKey1 || _.Key == scopeKey2)
            .Select(_ => _.Value.StringValue);
        Assert.Equal(2, otlpLogRecord.Attributes.Count);
        Assert.Equal(2, allScopeValues.Count());
        Assert.Contains(scopeValue1, allScopeValues);
        Assert.Contains(scopeValue2, allScopeValues);
    }

    [Theory]
    [InlineData("Same scope key", "Same scope key")]
    [InlineData("Scope key 1", "Scope key 2")]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndMultipleScopeLevelsAreAdded_ContainsAllAddedScopeValues(string scopeKey1, string scopeKey2)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeValue1 = "Some scope value";
        const string scopeValue2 = "Some other scope value";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>(scopeKey1, scopeValue1) }))
        {
            using (logger.BeginScope(new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>(scopeKey2, scopeValue2) }))
            {
                logger.LogInformation("Some log information message.");
            }
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        var allScopeValues = otlpLogRecord.Attributes
            .Where(_ => _.Key == scopeKey1 || _.Key == scopeKey2)
            .Select(_ => _.Value.StringValue);
        Assert.Equal(2, otlpLogRecord.Attributes.Count);
        Assert.Equal(2, allScopeValues.Count());
        Assert.Contains(scopeValue1, allScopeValues);
        Assert.Contains(scopeValue2, allScopeValues);
    }

    [Theory]
    [InlineData("Same scope key", "Same scope key")]
    [InlineData("Scope key 1", "Scope key 2")]
    public void ToOtlpLog_WhenOptionsIncludeScopesIsTrue_AndScopeIsUsedInLogMethod_ContainsAllAddedScopeValues(string scopeKey1, string scopeKey2)
    {
        // Arrange.
        var logRecords = new List<LogRecord>(1);
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.IncludeScopes = true;
                options.AddInMemoryExporter(logRecords);
            });
        });
        var logger = loggerFactory.CreateLogger(nameof(OtlpLogExporterTests));

        const string scopeValue1 = "Some scope value";
        const string scopeValue2 = "Some other scope value";

        // Act.
        using (logger.BeginScope(new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>(scopeKey1, scopeValue1),
        }))
        {
            logger.Log(
                LogLevel.Error,
                new EventId(1),
                new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>(scopeKey2, scopeValue2) },
                exception: new Exception("Some exception message"),
                formatter: (s, e) => string.Empty);
        }

        // Assert.
        var logRecord = logRecords.Single();
        var otlpLogRecordTransformer = new OtlpLogRecordTransformer(DefaultSdkLimitOptions, new());
        var otlpLogRecord = otlpLogRecordTransformer.ToOtlpLog(logRecord);
        var allScopeValues = otlpLogRecord.Attributes
            .Where(_ => _.Key == scopeKey1 || _.Key == scopeKey2)
            .Select(_ => _.Value.StringValue);
        Assert.Equal(2, otlpLogRecord.Attributes.Count);
        Assert.Equal(2, allScopeValues.Count());
        Assert.Contains(scopeValue1, allScopeValues);
        Assert.Contains(scopeValue2, allScopeValues);
    }

    [Fact]
    public void AddOtlpLogExporterDefaultOptionsTest()
    {
        var options = new OpenTelemetryLoggerOptions();

        options.AddOtlpExporter();

        var processors = (List<BaseProcessor<LogRecord>>)typeof(OpenTelemetryLoggerOptions).GetField("Processors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(options);

        Assert.Single(processors);

        var batchProcesor = processors[0] as BatchLogRecordExportProcessor;

        Assert.NotNull(batchProcesor);

        var batchProcessorType = typeof(BatchExportProcessor<LogRecord>);

        Assert.Equal(5000, batchProcessorType.GetField("scheduledDelayMilliseconds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(batchProcesor));
    }

    [Theory]
    [InlineData(ExportProcessorType.Simple)]
    [InlineData(ExportProcessorType.Batch)]
    public void AddOtlpLogExporterLogRecordProcessorOptionsTest(ExportProcessorType processorType)
    {
        var options = new OpenTelemetryLoggerOptions();

        options.AddOtlpExporter((o, l) =>
        {
            l.ExportProcessorType = processorType;
            l.BatchExportProcessorOptions = new BatchExportLogRecordProcessorOptions() { ScheduledDelayMilliseconds = 1000 };
        });

        var processors = (List<BaseProcessor<LogRecord>>)typeof(OpenTelemetryLoggerOptions).GetField("Processors", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(options);

        Assert.Single(processors);

        if (processorType == ExportProcessorType.Batch)
        {
            var batchProcesor = processors[0] as BatchLogRecordExportProcessor;

            Assert.NotNull(batchProcesor);

            var batchProcessorType = typeof(BatchExportProcessor<LogRecord>);

            Assert.Equal(1000, batchProcessorType.GetField("scheduledDelayMilliseconds", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(batchProcesor));
        }
        else
        {
            var simpleProcesor = processors[0] as SimpleLogRecordExportProcessor;

            Assert.NotNull(simpleProcesor);
        }
    }

    private static OtlpCommon.KeyValue TryGetAttribute(OtlpLogs.LogRecord record, string key)
    {
        return record.Attributes.FirstOrDefault(att => att.Key == key);
    }
}
