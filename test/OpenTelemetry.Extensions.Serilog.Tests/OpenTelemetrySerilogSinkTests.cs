// <copyright file="OpenTelemetrySerilogSinkTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Serilog;
using Xunit;
using ILogger = Serilog.ILogger;

namespace OpenTelemetry.Extensions.Serilog.Tests
{
    public class OpenTelemetrySerilogSinkTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SerilogDisposesProviderTests(bool dispose)
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new WrappedOpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            Log.Logger = new LoggerConfiguration()
                .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: dispose)
                .CreateLogger();

            Log.CloseAndFlush();

            Assert.Equal(dispose, openTelemetryLoggerProvider.Disposed);

            if (!dispose)
            {
                openTelemetryLoggerProvider.Dispose();
            }

            Assert.True(openTelemetryLoggerProvider.Disposed);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SerilogBasicLogTests(bool includeFormattedMessage)
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
            {
                options.IncludeFormattedMessage = includeFormattedMessage;

                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            Log.Logger = new LoggerConfiguration()
                .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true)
                .CreateLogger();

            Log.Logger.Information("Hello {greeting}", "World");

            Log.CloseAndFlush();

            Assert.Single(exportedItems);

            LogRecord logRecord = exportedItems[0];

            if (!includeFormattedMessage)
            {
                Assert.Equal("Hello {greeting}", logRecord.FormattedMessage);
            }
            else
            {
                Assert.Equal("Hello \"World\"", logRecord.FormattedMessage);
            }

            Assert.NotEqual(DateTime.MinValue, logRecord.Timestamp);
            Assert.Equal(DateTimeKind.Utc, logRecord.Timestamp.Kind);
            Assert.Equal(LogLevel.Information, logRecord.LogLevel);
            Assert.Null(logRecord.CategoryName);

            Assert.NotNull(logRecord.StateValues);
            Assert.Single(logRecord.StateValues);
            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "greeting" && (string?)kvp.Value == "World");
        }

        [Fact]
        public void SerilogCategoryNameTest()
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            Log.Logger = new LoggerConfiguration()
                .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true)
                .CreateLogger();

            // Note: Serilog ForContext API is used to set "CategoryName" on log messages
            ILogger logger = Log.Logger.ForContext<OpenTelemetrySerilogSinkTests>();

            logger.Information("Hello {greeting}", "World");

            Log.CloseAndFlush();

            Assert.Single(exportedItems);

            LogRecord logRecord = exportedItems[0];

            Assert.Equal("OpenTelemetry.Extensions.Serilog.Tests.OpenTelemetrySerilogSinkTests", logRecord.CategoryName);
        }

        [Fact]
        public void SerilogComplexMessageTemplateTest()
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            Log.Logger = new LoggerConfiguration()
                .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true)
                .CreateLogger();

            ComplexType complexType = new();

            Log.Logger.Information("Hello {greeting} {id} {@complexObj} {$complexStr}", "World", 18, complexType, complexType);

            Log.CloseAndFlush();

            Assert.Single(exportedItems);

            LogRecord logRecord = exportedItems[0];

            Assert.NotNull(logRecord.StateValues);
            Assert.Equal(3, logRecord.StateValues!.Count); // Note: complexObj is currently not supported/ignored.
            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "greeting" && (string?)kvp.Value == "World");
            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "id" && (int?)kvp.Value == 18);
            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "complexStr" && (string?)kvp.Value == "ComplexTypeToString");
        }

        [Fact]
        public void SerilogArrayMessageTemplateTest()
        {
            List<LogRecord> exportedItems = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            Log.Logger = new LoggerConfiguration()
                .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true)
                .CreateLogger();

            ComplexType complexType = new();

            var intArray = new int[] { 0, 1, 2, 3, 4 };
            var mixedArray = new object?[] { 0, null, "3", 18.0D };

            Log.Logger.Information("Int array {data}", intArray);
            Log.Logger.Information("Mixed array {data}", new object[] { mixedArray });

            Log.CloseAndFlush();

            Assert.Equal(2, exportedItems.Count);

            LogRecord logRecord = exportedItems[0];

            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "data" && kvp.Value is int[] typedArray && intArray.SequenceEqual(typedArray));

            logRecord = exportedItems[1];

            Assert.Contains(logRecord.StateValues, kvp => kvp.Key == "data" && kvp.Value is object?[] typedArray && mixedArray.SequenceEqual(typedArray));
        }

        [Fact]
        public void SerilogExceptionTest()
        {
            List<LogRecord> exportedItems = new();

            InvalidOperationException ex = new();

#pragma warning disable CA2000 // Dispose objects before losing scope
            var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
            {
                options.AddInMemoryExporter(exportedItems);
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            Log.Logger = new LoggerConfiguration()
                .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true)
                .CreateLogger();

            ComplexType complexType = new();

            Log.Logger.Information(ex, "Exception");

            Log.CloseAndFlush();

            Assert.Single(exportedItems);

            LogRecord logRecord = exportedItems[0];

            Assert.Equal(ex, logRecord.Exception);
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
    }
}
