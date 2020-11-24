// <copyright file="LogRecordTest.cs" company="OpenTelemetry Authors">
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

#if !NET452 && !NET46
#if NETCOREAPP2_1
using Microsoft.Extensions.DependencyInjection;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Logs
{
    public class LogRecordTest : IDisposable
    {
        private readonly ILogger logger;
        private readonly List<LogRecord> exportedItems = new List<LogRecord>();
#if NETCOREAPP2_1
        private readonly ServiceProvider serviceProvider;
#else
        private readonly ILoggerFactory loggerFactory;
#endif
        private readonly BaseExportProcessor<LogRecord> processor;
        private readonly BaseExporter<LogRecord> exporter;

        public LogRecordTest()
        {
            this.exporter = new InMemoryExporter<LogRecord>(this.exportedItems);
            this.processor = new SimpleExportProcessor<LogRecord>(this.exporter);
#if NETCOREAPP2_1
            var serviceCollection = new ServiceCollection().AddLogging(builder =>
#else
            this.loggerFactory = LoggerFactory.Create(builder =>
#endif
            {
                builder.AddOpenTelemetry(options => options
                    .AddProcessor(this.processor));
                builder.AddFilter(typeof(LogRecordTest).FullName, LogLevel.Trace);
            });

#if NETCOREAPP2_1
            this.serviceProvider = serviceCollection.BuildServiceProvider();
            this.logger = this.serviceProvider.GetRequiredService<ILogger<LogRecordTest>>();
#else
            this.logger = this.loggerFactory.CreateLogger<LogRecordTest>();
#endif
        }

        [Fact]
        public void CheckCateogryNameForLog()
        {
            this.logger.LogInformation("Log");
            var categoryName = this.exportedItems[0].CategoryName;

            Assert.Equal(typeof(LogRecordTest).FullName, categoryName);
        }

        [Theory]
        [InlineData(LogLevel.Trace)]
        [InlineData(LogLevel.Debug)]
        [InlineData(LogLevel.Information)]
        [InlineData(LogLevel.Warning)]
        [InlineData(LogLevel.Error)]
        [InlineData(LogLevel.Critical)]
        public void CheckLogLevel(LogLevel logLevel)
        {
            var message = $"Log {logLevel}";
            this.logger.Log(logLevel, message);

            var logLevelRecorded = this.exportedItems[0].LogLevel;
            Assert.Equal(logLevel, logLevelRecorded);
        }

        [Fact]
        public void CheckStateForUnstructuredLog()
        {
            var message = "Hello, World!";
            this.logger.LogInformation(message);
            var state = this.exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;

            // state only has {OriginalFormat}
            Assert.Equal(1, state.Count);

            Assert.Equal(message.ToString(), state.ToString());
        }

        [Fact]
        public void CheckStateForUnstructuredLogWithStringInterpolation()
        {
            var message = $"Hello from potato {0.99}.";
            this.logger.LogInformation(message);
            var state = this.exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;

            // state only has {OriginalFormat}
            Assert.Equal(1, state.Count);

            Assert.Equal(message.ToString(), state.ToString());
        }

        [Fact]
        public void CheckStateForStructuredLogWithTemplate()
        {
            var message = "Hello from {name} {price}.";
            this.logger.LogInformation(message, "tomato", 2.99);
            var state = this.exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;

            // state has name, price and {OriginalFormat}
            Assert.Equal(3, state.Count);

            // Check if first item is name
            Assert.Equal("name", state[0].Key);
            Assert.Equal("tomato", state[0].Value);

            // Check if second item is price
            Assert.Equal("price", state[1].Key);
            Assert.Equal(2.99, state[1].Value);

            // Check if third item is {OriginalFormat}
            Assert.Equal("{OriginalFormat}", state[2].Key);
            Assert.Equal(message, state[2].Value);

            Assert.Equal($"Hello from tomato 2.99.", state.ToString());
        }

        [Fact]
        public void CheckStateForStructuredLogWithStrongType()
        {
            var food = new Food { Name = "artichoke", Price = 3.99 };
            this.logger.LogInformation("{food}", food);
            var state = this.exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;

            // state only has food and {OriginalFormat}
            Assert.Equal(2, state.Count);

            // Check if the first item is food
            var stateFirstItem = (Food)state[0].Value;
            Assert.Equal("food", state[0].Key);
            Assert.Equal(food.Name, stateFirstItem.Name);
            Assert.Equal(food.Price, stateFirstItem.Price);

            // Check if second item is {OriginalFormat}
            Assert.Equal("{OriginalFormat}", state[1].Key);
            Assert.Equal("{food}", state[1].Value);

            Assert.Equal(food.ToString(), state.ToString());
        }

        [Fact]
        public void CheckStateForStructuredLogWithAnonymousType()
        {
            var anonymousType = new { Name = "pumpkin", Price = 5.99 };
            this.logger.LogInformation("{food}", anonymousType);
            var state = this.exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;

            // state only has food and {OriginalFormat}
            Assert.Equal(2, state.Count);

            // Check if the first item is the anonymous type logged
            var stateFirstItem = state[0].Value as dynamic;

            Assert.Equal("food", state[0].Key);
            Assert.Equal(anonymousType.Name, stateFirstItem.Name);
            Assert.Equal(anonymousType.Price, stateFirstItem.Price);

            // Check if the second item is {OriginalFormat}
            Assert.Equal("{OriginalFormat}", state[1].Key);
            Assert.Equal("{food}", state[1].Value);

            Assert.Equal(anonymousType.ToString(), state.ToString());
        }

        [Fact]
        public void CheckStateForStrucutredLogWithGeneralType()
        {
            var food = new Dictionary<string, object>
            {
                ["Name"] = "truffle",
                ["Price"] = 299.99,
            };
            this.logger.LogInformation("{food}", food);
            var state = this.exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;

            // state only has food and {OriginalFormat}
            Assert.Equal(2, state.Count);

            // Check if first item is food
            var stateFirstItem = state[0].Value as Dictionary<string, object>;
            Assert.Equal("food", state[0].Key);
            Assert.True(food.Count == stateFirstItem.Count && !food.Except(stateFirstItem).Any());

            // Check if the second item is {OriginalFormat}
            Assert.Equal("{OriginalFormat}", state[1].Key);
            Assert.Equal("{food}", state[1].Value);

            Assert.Equal("[Name, truffle], [Price, 299.99]", state.ToString());
        }

        [Fact]
        public void CheckStateForExceptionLogged()
        {
            var exceptionMessage = "Exception Message";
            var exception = new Exception(exceptionMessage);
            var message = "Exception Occurred";
            this.logger.LogInformation(exception, message);

            var state = this.exportedItems[0].State;
            var itemCount = state.GetType().GetProperty("Count").GetValue(state);

            // state only has {OriginalFormat}
            Assert.Equal(1, itemCount);

            var loggedException = this.exportedItems[0].Exception;
            Assert.NotNull(loggedException);
            Assert.Equal(exceptionMessage, loggedException.Message);

            Assert.Equal(message.ToString(), state.ToString());
        }

        [Fact]
        public void CheckTraceIdForLogWithinDroppedActivity()
        {
            this.logger.LogInformation("Log within a dropped activity");
            var logRecord = this.exportedItems[0];

            Assert.Null(Activity.Current);
            Assert.Equal(default, logRecord.TraceId);
            Assert.Equal(default, logRecord.SpanId);
            Assert.Equal(default, logRecord.TraceFlags);
        }

        [Fact]
        public void CheckTraceIdForLogWithinActivityMarkedAsRecordOnly()
        {
            var sampler = new RecordOnlySampler();
            var exportedActivityList = new List<Activity>();
            var activitySourceName = "LogRecordTest";
            var activitySource = new ActivitySource(activitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
                .SetSampler(sampler)
                .AddInMemoryExporter(exportedActivityList)
                .Build();

            using var activity = activitySource.StartActivity("Activity");

            this.logger.LogInformation("Log within activity marked as RecordOnly");
            var logRecord = this.exportedItems[0];

            var currentActivity = Activity.Current;
            Assert.NotNull(Activity.Current);
            Assert.Equal(currentActivity.TraceId, logRecord.TraceId);
            Assert.Equal(currentActivity.SpanId, logRecord.SpanId);
            Assert.Equal(currentActivity.ActivityTraceFlags, logRecord.TraceFlags);
        }

        [Fact]
        public void CheckTraceIdForLogWithinActivityMarkedAsRecordAndSample()
        {
            var sampler = new AlwaysOnSampler();
            var exportedActivityList = new List<Activity>();
            var activitySourceName = "LogRecordTest";
            var activitySource = new ActivitySource(activitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
                .SetSampler(sampler)
                .AddInMemoryExporter(exportedActivityList)
                .Build();

            using var activity = activitySource.StartActivity("Activity");

            this.logger.LogInformation("Log within activity marked as RecordAndSample");
            var logRecord = this.exportedItems[0];

            var currentActivity = Activity.Current;
            Assert.NotNull(Activity.Current);
            Assert.Equal(currentActivity.TraceId, logRecord.TraceId);
            Assert.Equal(currentActivity.SpanId, logRecord.SpanId);
            Assert.Equal(currentActivity.ActivityTraceFlags, logRecord.TraceFlags);
        }

        public void Dispose()
        {
#if NETCOREAPP2_1
            this.serviceProvider?.Dispose();
#else
            this.loggerFactory?.Dispose();
#endif
        }

        internal struct Food
        {
            public string Name { get; set; }

            public double Price { get; set; }
        }
    }
}
#endif
