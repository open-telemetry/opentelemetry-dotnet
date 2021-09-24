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
#if !NET461
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Logs.Tests
{
    public sealed class LogRecordTest : IDisposable
    {
        private readonly ILogger logger;
        private readonly List<LogRecord> exportedItems = new List<LogRecord>();
        private readonly ILoggerFactory loggerFactory;
        private readonly BaseExportProcessor<LogRecord> processor;
        private readonly BaseExporter<LogRecord> exporter;
        private OpenTelemetryLoggerOptions options;

        public LogRecordTest()
        {
            this.exporter = new InMemoryExporter<LogRecord>(this.exportedItems);
            this.processor = new TestLogRecordProcessor(this.exporter);
            this.loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    this.options = options;
                    options
                        .AddProcessor(this.processor);
                });
                builder.AddFilter(typeof(LogRecordTest).FullName, LogLevel.Trace);
            });

            this.logger = this.loggerFactory.CreateLogger<LogRecordTest>();
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

            // Check if state has name
            Assert.Contains(state, item => item.Key == "name");
            Assert.Equal("tomato", state.First(item => item.Key == "name").Value);

            // Check if state has price
            Assert.Contains(state, item => item.Key == "price");
            Assert.Equal(2.99, state.First(item => item.Key == "price").Value);

            // Check if state has OriginalFormat
            Assert.Contains(state, item => item.Key == "{OriginalFormat}");
            Assert.Equal(message, state.First(item => item.Key == "{OriginalFormat}").Value);

            Assert.Equal($"Hello from tomato 2.99.", state.ToString());
        }

        [Fact]
        public void CheckStateForStructuredLogWithStrongType()
        {
            var food = new Food { Name = "artichoke", Price = 3.99 };
            this.logger.LogInformation("{food}", food);
            var state = this.exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;

            // state has food and {OriginalFormat}
            Assert.Equal(2, state.Count);

            // Check if state has food
            Assert.Contains(state, item => item.Key == "food");

            var foodParameter = (Food)state.First(item => item.Key == "food").Value;
            Assert.Equal(food.Name, foodParameter.Name);
            Assert.Equal(food.Price, foodParameter.Price);

            // Check if state has OriginalFormat
            Assert.Contains(state, item => item.Key == "{OriginalFormat}");
            Assert.Equal("{food}", state.First(item => item.Key == "{OriginalFormat}").Value);

            Assert.Equal(food.ToString(), state.ToString());
        }

        [Fact]
        public void CheckStateForStructuredLogWithAnonymousType()
        {
            var anonymousType = new { Name = "pumpkin", Price = 5.99 };
            this.logger.LogInformation("{food}", anonymousType);
            var state = this.exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;

            // state has food and {OriginalFormat}
            Assert.Equal(2, state.Count);

            // Check if state has food
            Assert.Contains(state, item => item.Key == "food");

            var foodParameter = state.First(item => item.Key == "food").Value as dynamic;
            Assert.Equal(anonymousType.Name, foodParameter.Name);
            Assert.Equal(anonymousType.Price, foodParameter.Price);

            // Check if state has OriginalFormat
            Assert.Contains(state, item => item.Key == "{OriginalFormat}");
            Assert.Equal("{food}", state.First(item => item.Key == "{OriginalFormat}").Value);

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

            // Check if state has food
            Assert.Contains(state, item => item.Key == "food");

            var foodParameter = state.First(item => item.Key == "food").Value as Dictionary<string, object>;
            Assert.True(food.Count == foodParameter.Count && !food.Except(foodParameter).Any());

            // Check if state has OriginalFormat
            Assert.Contains(state, item => item.Key == "{OriginalFormat}");
            Assert.Equal("{food}", state.First(item => item.Key == "{OriginalFormat}").Value);

            var prevCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                Assert.Equal("[Name, truffle], [Price, 299.99]", state.ToString());
            }
            finally
            {
                CultureInfo.CurrentCulture = prevCulture;
            }
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

        [Fact]
        public void IncludeFormattedMessageTest()
        {
            this.logger.LogInformation("OpenTelemetry!");
            var logRecord = this.exportedItems[0];
            Assert.Null(logRecord.FormattedMessage);

            this.options.IncludeFormattedMessage = true;
            try
            {
                this.logger.LogInformation("OpenTelemetry!");
                logRecord = this.exportedItems[1];
                Assert.Equal("OpenTelemetry!", logRecord.FormattedMessage);

                this.logger.LogInformation("OpenTelemetry {Greeting} {Subject}!", "Hello", "World");
                logRecord = this.exportedItems[2];
                Assert.Equal("OpenTelemetry Hello World!", logRecord.FormattedMessage);
            }
            finally
            {
                this.options.IncludeFormattedMessage = false;
            }
        }

        [Fact]
        public void IncludeFormattedMessageTestWhenFormatterNull()
        {
            this.logger.Log(LogLevel.Information, default, "Hello World!", null, null);
            var logRecord = this.exportedItems[0];
            Assert.Null(logRecord.FormattedMessage);

            this.options.IncludeFormattedMessage = true;
            try
            {
                // Pass null as formatter function
                this.logger.Log(LogLevel.Information, default, "Hello World!", null, null);
                logRecord = this.exportedItems[1];
                Assert.Null(logRecord.FormattedMessage);

                var expectedFormattedMessage = "formatted message";
                this.logger.Log(LogLevel.Information, default, "Hello World!", null, (state, ex) => expectedFormattedMessage);
                logRecord = this.exportedItems[2];
                Assert.Equal(expectedFormattedMessage, logRecord.FormattedMessage);
            }
            finally
            {
                this.options.IncludeFormattedMessage = false;
            }
        }

        [Fact]
        public void IncludeScopesTest()
        {
            using var scope = this.logger.BeginScope("string_scope");

            this.logger.LogInformation("OpenTelemetry!");
            var logRecord = this.exportedItems[0];

            List<object> scopes = new List<object>();
            logRecord.ForEachScope<object>((scope, state) => scopes.Add(scope.Scope), null);
            Assert.Empty(scopes);

            this.options.IncludeScopes = true;
            try
            {
                this.logger.LogInformation("OpenTelemetry!");
                logRecord = this.exportedItems[1];

                int reachedDepth = -1;
                logRecord.ForEachScope<object>(
                    (scope, state) =>
                    {
                        reachedDepth++;
                        scopes.Add(scope.Scope);
                        foreach (KeyValuePair<string, object> item in scope)
                        {
                            Assert.Equal(string.Empty, item.Key);
                            Assert.Equal("string_scope", item.Value);
                        }
                    },
                    null);
                Assert.Single(scopes);
                Assert.Equal(0, reachedDepth);
                Assert.Equal("string_scope", scopes[0]);

                scopes.Clear();

                List<KeyValuePair<string, object>> expectedScope2 = new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("item1", "value1"),
                    new KeyValuePair<string, object>("item2", "value2"),
                };
                using var scope2 = this.logger.BeginScope(expectedScope2);

                this.logger.LogInformation("OpenTelemetry!");
                logRecord = this.exportedItems[2];

                reachedDepth = -1;
                logRecord.ForEachScope<object>(
                    (scope, state) =>
                    {
                        scopes.Add(scope.Scope);
                        if (reachedDepth++ == 1)
                        {
                            foreach (KeyValuePair<string, object> item in scope)
                            {
                                Assert.Contains(item, expectedScope2);
                            }
                        }
                    },
                    null);
                Assert.Equal(2, scopes.Count);
                Assert.Equal(1, reachedDepth);
                Assert.Equal("string_scope", scopes[0]);
                Assert.Same(expectedScope2, scopes[1]);

                scopes.Clear();

                KeyValuePair<string, object>[] expectedScope3 = new KeyValuePair<string, object>[]
                {
                    new KeyValuePair<string, object>("item3", "value3"),
                    new KeyValuePair<string, object>("item4", "value4"),
                };
                using var scope3 = this.logger.BeginScope(expectedScope3);

                this.logger.LogInformation("OpenTelemetry!");
                logRecord = this.exportedItems[3];

                reachedDepth = -1;
                logRecord.ForEachScope<object>(
                    (scope, state) =>
                    {
                        scopes.Add(scope.Scope);
                        if (reachedDepth++ == 2)
                        {
                            foreach (KeyValuePair<string, object> item in scope)
                            {
                                Assert.Contains(item, expectedScope3);
                            }
                        }
                    },
                    null);
                Assert.Equal(3, scopes.Count);
                Assert.Equal(2, reachedDepth);
                Assert.Equal("string_scope", scopes[0]);
                Assert.Same(expectedScope2, scopes[1]);
                Assert.Same(expectedScope3, scopes[2]);
            }
            finally
            {
                this.options.IncludeScopes = false;
            }
        }

        [Fact]
        public void ParseStateValuesUsingStandardExtensionsTest()
        {
            // Tests state parsing with standard extensions.

            this.logger.LogInformation("{Product} {Year}!", "OpenTelemetry", 2021);
            var logRecord = this.exportedItems[0];

            Assert.NotNull(logRecord.State);
            Assert.Null(logRecord.StateValues);

            this.options.ParseStateValues = true;
            try
            {
                var complex = new { Property = "Value" };

                this.logger.LogInformation("{Product} {Year} {Complex}!", "OpenTelemetry", 2021, complex);
                logRecord = this.exportedItems[1];

                Assert.Null(logRecord.State);
                Assert.NotNull(logRecord.StateValues);
                Assert.Equal(4, logRecord.StateValues.Count);
                Assert.Equal(new KeyValuePair<string, object>("Product", "OpenTelemetry"), logRecord.StateValues[0]);
                Assert.Equal(new KeyValuePair<string, object>("Year", 2021), logRecord.StateValues[1]);
                Assert.Equal(new KeyValuePair<string, object>("{OriginalFormat}", "{Product} {Year} {Complex}!"), logRecord.StateValues[3]);

                KeyValuePair<string, object> actualComplex = logRecord.StateValues[2];
                Assert.Equal("Complex", actualComplex.Key);
                Assert.Same(complex, actualComplex.Value);
            }
            finally
            {
                this.options.ParseStateValues = false;
            }
        }

        [Fact]
        public void ParseStateValuesUsingStructTest()
        {
            // Tests struct IReadOnlyList<KeyValuePair<string, object>> parse path.

            this.options.ParseStateValues = true;
            try
            {
                this.logger.Log(
                    LogLevel.Information,
                    0,
                    new StructState(new KeyValuePair<string, object>("Key1", "Value1")),
                    null,
                    (s, e) => "OpenTelemetry!");
                var logRecord = this.exportedItems[0];

                Assert.Null(logRecord.State);
                Assert.NotNull(logRecord.StateValues);
                Assert.Equal(1, logRecord.StateValues.Count);
                Assert.Equal(new KeyValuePair<string, object>("Key1", "Value1"), logRecord.StateValues[0]);
            }
            finally
            {
                this.options.ParseStateValues = false;
            }
        }

        [Fact]
        public void ParseStateValuesUsingListTest()
        {
            // Tests ref IReadOnlyList<KeyValuePair<string, object>> parse path.

            this.options.ParseStateValues = true;
            try
            {
                this.logger.Log(
                    LogLevel.Information,
                    0,
                    new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("Key1", "Value1") },
                    null,
                    (s, e) => "OpenTelemetry!");
                var logRecord = this.exportedItems[0];

                Assert.Null(logRecord.State);
                Assert.NotNull(logRecord.StateValues);
                Assert.Equal(1, logRecord.StateValues.Count);
                Assert.Equal(new KeyValuePair<string, object>("Key1", "Value1"), logRecord.StateValues[0]);
            }
            finally
            {
                this.options.ParseStateValues = false;
            }
        }

        [Fact]
        public void ParseStateValuesUsingIEnumerableTest()
        {
            // Tests IEnumerable<KeyValuePair<string, object>> parse path.

            this.options.ParseStateValues = true;
            try
            {
                this.logger.Log(
                    LogLevel.Information,
                    0,
                    new ListState(new KeyValuePair<string, object>("Key1", "Value1")),
                    null,
                    (s, e) => "OpenTelemetry!");
                var logRecord = this.exportedItems[0];

                Assert.Null(logRecord.State);
                Assert.NotNull(logRecord.StateValues);
                Assert.Equal(1, logRecord.StateValues.Count);
                Assert.Equal(new KeyValuePair<string, object>("Key1", "Value1"), logRecord.StateValues[0]);
            }
            finally
            {
                this.options.ParseStateValues = false;
            }
        }

        [Fact]
        public void ParseStateValuesUsingCustomTest()
        {
            // Tests unknown state parse path.

            this.options.ParseStateValues = true;
            try
            {
                CustomState state = new CustomState
                {
                    Property = "Value",
                };

                this.logger.Log(
                    LogLevel.Information,
                    0,
                    state,
                    null,
                    (s, e) => "OpenTelemetry!");
                var logRecord = this.exportedItems[0];

                Assert.Null(logRecord.State);
                Assert.NotNull(logRecord.StateValues);
                Assert.Equal(1, logRecord.StateValues.Count);

                KeyValuePair<string, object> actualState = logRecord.StateValues[0];

                Assert.Equal(string.Empty, actualState.Key);
                Assert.Same(state, actualState.Value);
            }
            finally
            {
                this.options.ParseStateValues = false;
            }
        }

        public void Dispose()
        {
            this.loggerFactory?.Dispose();
        }

        internal struct Food
        {
            public string Name { get; set; }

            public double Price { get; set; }
        }

        private struct StructState : IReadOnlyList<KeyValuePair<string, object>>
        {
            private readonly List<KeyValuePair<string, object>> list;

            public StructState(params KeyValuePair<string, object>[] items)
            {
                this.list = new List<KeyValuePair<string, object>>(items);
            }

            public int Count => this.list.Count;

            public KeyValuePair<string, object> this[int index] => this.list[index];

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                return this.list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.list.GetEnumerator();
            }
        }

        private class ListState : IEnumerable<KeyValuePair<string, object>>
        {
            private readonly List<KeyValuePair<string, object>> list;

            public ListState(params KeyValuePair<string, object>[] items)
            {
                this.list = new List<KeyValuePair<string, object>>(items);
            }

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                return this.list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.list.GetEnumerator();
            }
        }

        private class CustomState
        {
            public string Property { get; set; }
        }

        private class TestLogRecordProcessor : SimpleExportProcessor<LogRecord>
        {
            public TestLogRecordProcessor(BaseExporter<LogRecord> exporter)
                : base(exporter)
            {
            }

            public override void OnEnd(LogRecord data)
            {
                data.BufferLogScopes();

                base.OnEnd(data);
            }
        }
    }
}
#endif
