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

        [Fact]
        public void CheckLogLevelForTrace()
        {
            var message = "Log Trace";
            this.logger.LogTrace(message);

            var logLevel = this.exportedItems[0].LogLevel;
            Assert.Equal(LogLevel.Trace, logLevel);
        }

        [Fact]
        public void CheckLogLevelForDebug()
        {
            var message = "Log Debug";
            this.logger.LogDebug(message);

            var logLevel = this.exportedItems[0].LogLevel;
            Assert.Equal(LogLevel.Debug, logLevel);
        }

        [Fact]
        public void CheckLogLevelForInformation()
        {
            var message = "Log Information";
            this.logger.LogInformation(message);

            var logLevel = this.exportedItems[0].LogLevel;
            Assert.Equal(LogLevel.Information, logLevel);
        }

        [Fact]
        public void CheckLogLevelForWarning()
        {
            var message = "Log Warning";
            this.logger.LogWarning(message);

            var logLevel = this.exportedItems[0].LogLevel;
            Assert.Equal(LogLevel.Warning, logLevel);
        }

        [Fact]
        public void CheckLogLevelForError()
        {
            var message = "Log Error";
            this.logger.LogError(message);

            var logLevel = this.exportedItems[0].LogLevel;
            Assert.Equal(LogLevel.Error, logLevel);
        }

        [Fact]
        public void CheckLogLevelForCritical()
        {
            var message = "Log Critical";
            this.logger.LogCritical(message);

            var logLevel = this.exportedItems[0].LogLevel;
            Assert.Equal(LogLevel.Critical, logLevel);
        }

        [Fact]
        public void CheckLogLevelForNone()
        {
            var message = "Log None";
            this.logger.Log(LogLevel.None, message);

            var logLevel = this.exportedItems[0].LogLevel;
            Assert.Equal(LogLevel.None, logLevel);
        }

        [Fact]
        public void CheckStateForUnstructuredLog()
        {
            var message = "Hello, World!";
            this.logger.LogInformation(message);
            var state = this.exportedItems[0].State;
            var itemCount = state.GetType().GetProperty("Count").GetValue(state);

            // state only has {OriginalFormat}
            Assert.Equal(1, itemCount);

            Assert.Equal(message.ToString(), state.ToString());
        }

        [Fact]
        public void CheckStateForUnstructuredLogWithStringInterpolation()
        {
            var message = $"Hello from potato {0.99}.";
            this.logger.LogInformation(message);
            var state = this.exportedItems[0].State;
            var itemCount = state.GetType().GetProperty("Count").GetValue(state);

            // state only has {OriginalFormat}
            Assert.Equal(1, itemCount);

            Assert.Equal(message.ToString(), state.ToString());
        }

        [Fact]
        public void CheckStateForStructuredLogWithTemplate()
        {
            this.logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
            var state = this.exportedItems[0].State;
            var itemCount = state.GetType().GetProperty("Count").GetValue(state);

            // state has name, price and {OriginalFormat}
            Assert.Equal(3, itemCount);

            // Get value for {name}
            var firstArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 0 });
            var firstArgumentKey = firstArgument.GetType().GetProperty("Key").GetValue(firstArgument).ToString();
            var firstArgumentValue = firstArgument.GetType().GetProperty("Value").GetValue(firstArgument).ToString();

            Assert.Equal("name", firstArgumentKey);
            Assert.Equal("tomato", firstArgumentValue);

            // Get value for {price}
            var secondArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 1 });
            var secondArgumentKey = secondArgument.GetType().GetProperty("Key").GetValue(secondArgument).ToString();
            var secondArgumentValue = Convert.ToDouble(secondArgument.GetType().GetProperty("Value").GetValue(secondArgument));

            Assert.Equal("price", secondArgumentKey);
            Assert.Equal(2.99, secondArgumentValue);

            // Get value for {OriginalFormat}
            var thirdArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 2 });
            var thirdArgumentKey = thirdArgument.GetType().GetProperty("Key").GetValue(thirdArgument).ToString();
            var thirdArgumentValue = thirdArgument.GetType().GetProperty("Value").GetValue(thirdArgument).ToString();
            Assert.Equal("{OriginalFormat}", thirdArgumentKey);
            Assert.Equal("Hello from {name} {price}.", thirdArgumentValue);

            Assert.Equal($"Hello from tomato 2.99.", state.ToString());
        }

        [Fact]
        public void CheckStateForStructuredLogWithStrongType()
        {
            var food = new Food { Name = "artichoke", Price = 3.99 };
            this.logger.LogInformation("{food}", food);
            var state = this.exportedItems[0].State;
            var itemCount = state.GetType().GetProperty("Count").GetValue(state);

            // state only has food and {OriginalFormat}
            Assert.Equal(2, itemCount);

            // Get value for food
            var firstArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 0 });
            var firstArgumentKey = firstArgument.GetType().GetProperty("Key").GetValue(firstArgument).ToString();
            var firstArgumentValue = (Food)firstArgument.GetType().GetProperty("Value").GetValue(firstArgument);

            Assert.Equal("food", firstArgumentKey);
            Assert.Equal(food.Name, firstArgumentValue.Name);
            Assert.Equal(food.Price, firstArgumentValue.Price);

            // Get value for {OriginalFormat}
            var secondArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 1 });
            var secondArgumentKey = secondArgument.GetType().GetProperty("Key").GetValue(secondArgument).ToString();
            var secondArgumentValue = secondArgument.GetType().GetProperty("Value").GetValue(secondArgument).ToString();

            Assert.Equal("{OriginalFormat}", secondArgumentKey);
            Assert.Equal("{food}", secondArgumentValue);

            Assert.Equal(food.ToString(), state.ToString());
        }

        [Fact]
        public void CheckStateForStructuredLogWithAnonymousType()
        {
            var anonymousType = new { Name = "pumpkin", Price = 5.99 };
            this.logger.LogInformation("{food}", anonymousType);
            var state = this.exportedItems[0].State;
            var itemCount = state.GetType().GetProperty("Count").GetValue(state);

            // state only food and {OriginalFormat}
            Assert.Equal(2, itemCount);

            // Get value for {food}
            var firstArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 0 });
            var firstArgumentKey = firstArgument.GetType().GetProperty("Key").GetValue(firstArgument).ToString();
            var firstArgumentValue = firstArgument.GetType().GetProperty("Value").GetValue(firstArgument);
            var firstArgumentValueName = firstArgumentValue.GetType().GetProperty("Name").GetValue(firstArgumentValue).ToString();
            var firstArgumentValuePrice = Convert.ToDouble(firstArgumentValue.GetType().GetProperty("Price").GetValue(firstArgumentValue));

            Assert.Equal("food", firstArgumentKey);
            Assert.Equal(anonymousType.Name, firstArgumentValueName);
            Assert.Equal(anonymousType.Price, firstArgumentValuePrice);

            // Get value for {OriginalFormat}
            var secondArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 1 });
            var secondArgumentKey = secondArgument.GetType().GetProperty("Key").GetValue(secondArgument).ToString();
            var secondArgumentValue = secondArgument.GetType().GetProperty("Value").GetValue(secondArgument).ToString();

            Assert.Equal("{OriginalFormat}", secondArgumentKey);
            Assert.Equal("{food}", secondArgumentValue);

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
            var state = this.exportedItems[0].State;
            var itemCount = state.GetType().GetProperty("Count").GetValue(state);

            // state only food and {OriginalFormat}
            Assert.Equal(2, itemCount);

            // Get value for {food}
            var firstArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 0 });
            var firstArgumentKey = firstArgument.GetType().GetProperty("Key").GetValue(firstArgument).ToString();
            var firstArgumentValue = firstArgument.GetType().GetProperty("Value").GetValue(firstArgument) as Dictionary<string, object>;

            Assert.Equal("food", firstArgumentKey);
            Assert.True(food.Count == firstArgumentValue.Count && !food.Except(firstArgumentValue).Any());

            // Get value for {OriginalFormat}
            var secondArgument = state.GetType().GetProperty("Item").GetValue(state, new object[] { 1 });
            var secondArgumentKey = secondArgument.GetType().GetProperty("Key").GetValue(secondArgument).ToString();
            var secondArgumentValue = secondArgument.GetType().GetProperty("Value").GetValue(secondArgument).ToString();

            Assert.Equal("{OriginalFormat}", secondArgumentKey);
            Assert.Equal("{food}", secondArgumentValue);

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
            var sampler = new AlwaysOffSampler();
            var exportedActivityList = new List<Activity>();
            var activitySourceName = "LogRecordTest";
            var activitySource = new ActivitySource(activitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
                .SetSampler(sampler)
                .AddInMemoryExporter(exportedActivityList)
                .Build();

            using var activity = activitySource.StartActivity("Activity");

            this.logger.LogInformation("Log within a dropped activity");
            var logRecord = this.exportedItems[0];

            Assert.Equal(activity.TraceId, logRecord.TraceId);
            Assert.Equal(activity.SpanId, logRecord.SpanId);
            Assert.True(activity.ActivityTraceFlags.Equals(logRecord.TraceFlags));
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

            Assert.Equal(activity.TraceId, logRecord.TraceId);
            Assert.Equal(activity.SpanId, logRecord.SpanId);
            Assert.True(activity.ActivityTraceFlags.Equals(logRecord.TraceFlags));
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

            Assert.Equal(activity.TraceId, logRecord.TraceId);
            Assert.Equal(activity.SpanId, logRecord.SpanId);
            Assert.True(activity.ActivityTraceFlags.Equals(logRecord.TraceFlags));
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
