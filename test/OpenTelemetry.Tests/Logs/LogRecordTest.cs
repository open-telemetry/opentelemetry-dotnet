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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    public sealed class LogRecordTest
    {
        private enum Field
        {
            FormattedMessage,
            State,
            StateValues,
        }

        [Fact]
        public void CheckCategoryNameForLog()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            logger.LogInformation("Log");
            var categoryName = exportedItems[0].CategoryName;

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
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            const string message = "Log {logLevel}";
            logger.Log(logLevel, message, logLevel);

            var logLevelRecorded = exportedItems[0].LogLevel;
            Assert.Equal(logLevel, logLevelRecorded);
        }

        [Fact]
        public void CheckStateForUnstructuredLog()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            const string message = "Hello, World!";
            logger.LogInformation(message);

            Assert.Null(exportedItems[0].State);

            var attributes = exportedItems[0].Attributes;

            Assert.NotNull(attributes);

            // state only has {OriginalFormat}
            Assert.Equal(1, attributes.Count);

            Assert.Equal(message, exportedItems[0].Body);
            Assert.Null(exportedItems[0].FormattedMessage);
        }

        [Fact]
        [SuppressMessage("CA2254", "CA2254", Justification = "While you shouldn't use interpolation in a log message, this test verifies things work with it anyway.")]
        public void CheckStateForUnstructuredLogWithStringInterpolation()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            var message = $"Hello from potato {0.99}.";
            logger.LogInformation(message);

            Assert.Null(exportedItems[0].State);

            var attributes = exportedItems[0].Attributes;

            Assert.NotNull(attributes);

            // state only has {OriginalFormat}
            Assert.Equal(1, attributes.Count);

            Assert.Equal(message, exportedItems[0].Body);
            Assert.Null(exportedItems[0].FormattedMessage);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckStateForStructuredLogWithTemplate(bool includeFormattedMessage)
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeFormattedMessage = includeFormattedMessage);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            const string message = "Hello from {name} {price}.";
            logger.LogInformation(message, "tomato", 2.99);

            Assert.Null(exportedItems[0].State);

            var attributes = exportedItems[0].Attributes;

            Assert.NotNull(attributes);

            // state has name, price and {OriginalFormat}
            Assert.Equal(3, attributes.Count);

            // Check if state has name
            Assert.Contains(attributes, item => item.Key == "name");
            Assert.Equal("tomato", attributes.First(item => item.Key == "name").Value);

            // Check if state has price
            Assert.Contains(attributes, item => item.Key == "price");
            Assert.Equal(2.99, attributes.First(item => item.Key == "price").Value);

            // Check if state has OriginalFormat
            Assert.Contains(attributes, item => item.Key == "{OriginalFormat}");
            Assert.Equal(message, attributes.First(item => item.Key == "{OriginalFormat}").Value);

            Assert.Equal(message, exportedItems[0].Body);

            if (includeFormattedMessage)
            {
                Assert.Equal($"Hello from tomato 2.99.", exportedItems[0].FormattedMessage);
            }
            else
            {
                Assert.Null(exportedItems[0].FormattedMessage);
            }
        }

        [Fact]
        public void CheckStateForStructuredLogWithStrongType()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            var food = new Food { Name = "artichoke", Price = 3.99 };
            logger.LogInformation("{food}", food);

            Assert.Null(exportedItems[0].State);

            var attributes = exportedItems[0].Attributes;

            Assert.NotNull(attributes);

            // state has food and {OriginalFormat}
            Assert.Equal(2, attributes.Count);

            // Check if state has food
            Assert.Contains(attributes, item => item.Key == "food");

            var foodParameter = (Food)attributes.First(item => item.Key == "food").Value;
            Assert.Equal(food.Name, foodParameter.Name);
            Assert.Equal(food.Price, foodParameter.Price);

            // Check if state has OriginalFormat
            Assert.Contains(attributes, item => item.Key == "{OriginalFormat}");
            Assert.Equal("{food}", attributes.First(item => item.Key == "{OriginalFormat}").Value);

            Assert.Equal("{food}", exportedItems[0].Body);
        }

        [Fact]
        public void CheckStateForStructuredLogWithAnonymousType()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            var anonymousType = new { Name = "pumpkin", Price = 5.99 };
            logger.LogInformation("{food}", anonymousType);

            Assert.Null(exportedItems[0].State);

            var attributes = exportedItems[0].Attributes;

            Assert.NotNull(attributes);

            // state has food and {OriginalFormat}
            Assert.Equal(2, attributes.Count);

            // Check if state has food
            Assert.Contains(attributes, item => item.Key == "food");

            var foodParameter = attributes.First(item => item.Key == "food").Value as dynamic;
            Assert.Equal(anonymousType.Name, foodParameter.Name);
            Assert.Equal(anonymousType.Price, foodParameter.Price);

            // Check if state has OriginalFormat
            Assert.Contains(attributes, item => item.Key == "{OriginalFormat}");
            Assert.Equal("{food}", attributes.First(item => item.Key == "{OriginalFormat}").Value);

            Assert.Equal("{food}", exportedItems[0].Body);
        }

        [Fact]
        public void CheckStateForStructuredLogWithGeneralType()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeFormattedMessage = true);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            var food = new Dictionary<string, object>
            {
                ["Name"] = "truffle",
                ["Price"] = 299.99,
            };
            logger.LogInformation("{food}", food);

            Assert.Null(exportedItems[0].State);

            var attributes = exportedItems[0].Attributes;

            Assert.NotNull(attributes);

            // state only has food and {OriginalFormat}
            Assert.Equal(2, attributes.Count);

            // Check if state has food
            Assert.Contains(attributes, item => item.Key == "food");

            var foodParameter = attributes.First(item => item.Key == "food").Value as Dictionary<string, object>;
            Assert.True(food.Count == foodParameter.Count && !food.Except(foodParameter).Any());

            // Check if state has OriginalFormat
            Assert.Contains(attributes, item => item.Key == "{OriginalFormat}");
            Assert.Equal("{food}", attributes.First(item => item.Key == "{OriginalFormat}").Value);

            Assert.Equal("{food}", exportedItems[0].Body);

            var prevCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                Assert.Equal("[Name, truffle], [Price, 299.99]", exportedItems[0].FormattedMessage);
            }
            finally
            {
                CultureInfo.CurrentCulture = prevCulture;
            }
        }

        [Fact]
        public void CheckStateForExceptionLogged()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            var exceptionMessage = "Exception Message";
            var exception = new Exception(exceptionMessage);

            const string message = "Exception Occurred";
            logger.LogInformation(exception, message);

            Assert.Null(exportedItems[0].State);

            var attributes = exportedItems[0].Attributes;

            Assert.NotNull(attributes);

            var itemCount = attributes.Count;

            // state only has {OriginalFormat}
            Assert.Equal(1, itemCount);

            var loggedException = exportedItems[0].Exception;
            Assert.NotNull(loggedException);
            Assert.Equal(exceptionMessage, loggedException.Message);

            Assert.Null(exportedItems[0].FormattedMessage);

            Assert.Equal(message, exportedItems[0].Body);

            Assert.Equal("{OriginalFormat}", attributes[0].Key);
            Assert.Equal(message, attributes[0].Value);
        }

        [Fact]
        public void CheckStateCanBeSet()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            logger.LogInformation("This does not matter.");

            var logRecord = exportedItems[0];
            logRecord.State = "newState";

            var expectedState = "newState";
            Assert.Equal(expectedState, logRecord.State);
        }

        [Fact]
        public void CheckStateValuesCanBeSet()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = true);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            logger.Log(
                LogLevel.Information,
                0,
                new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("Key1", "Value1") },
                null,
                (s, e) => "OpenTelemetry!");

            var logRecord = exportedItems[0];
            var expectedStateValues = new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("Key2", "Value2") };
            logRecord.StateValues = expectedStateValues;

            Assert.Equal(expectedStateValues, logRecord.StateValues);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckFormattedMessageCanBeSet(bool includeFormattedMessage)
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeFormattedMessage = includeFormattedMessage);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            logger.LogInformation("OpenTelemetry {Greeting} {Subject}!", "Hello", "World");
            var logRecord = exportedItems[0];

            Assert.Equal("OpenTelemetry {Greeting} {Subject}!", logRecord.Body);
            if (includeFormattedMessage)
            {
                Assert.Equal("OpenTelemetry Hello World!", logRecord.FormattedMessage);
            }
            else
            {
                Assert.Null(logRecord.FormattedMessage);
            }

            var expectedFormattedMessage = "OpenTelemetry Good Night!";
            logRecord.FormattedMessage = expectedFormattedMessage;

            Assert.Equal(expectedFormattedMessage, logRecord.FormattedMessage);
        }

        [Fact]
        public void CheckStateCanBeSetByProcessor()
        {
            var exportedItems = new List<LogRecord>();
            var exporter = new InMemoryExporter<LogRecord>(exportedItems);
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddProcessor(new RedactionProcessor(Field.State));
                    options.AddInMemoryExporter(exportedItems);
                });
            });

            var logger = loggerFactory.CreateLogger<LogRecordTest>();
            logger.LogInformation($"This does not matter.");

            var state = exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;
            Assert.Equal("newStateKey", state[0].Key.ToString());
            Assert.Equal("newStateValue", state[0].Value.ToString());
        }

        [Fact]
        public void CheckStateValuesCanBeSetByProcessor()
        {
            var exportedItems = new List<LogRecord>();
            var exporter = new InMemoryExporter<LogRecord>(exportedItems);
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddProcessor(new RedactionProcessor(Field.StateValues));
                    options.AddInMemoryExporter(exportedItems);
                    options.ParseStateValues = true;
                });
            });

            var logger = loggerFactory.CreateLogger<LogRecordTest>();
            logger.LogInformation("This does not matter.");

            var stateValue = exportedItems[0];
            Assert.Equal(new KeyValuePair<string, object>("newStateValueKey", "newStateValueValue"), stateValue.StateValues[0]);
        }

        [Fact]
        public void CheckFormattedMessageCanBeSetByProcessor()
        {
            var exportedItems = new List<LogRecord>();
            var exporter = new InMemoryExporter<LogRecord>(exportedItems);
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.AddProcessor(new RedactionProcessor(Field.FormattedMessage));
                    options.AddInMemoryExporter(exportedItems);
                    options.IncludeFormattedMessage = true;
                });
            });

            var logger = loggerFactory.CreateLogger<LogRecordTest>();
            logger.LogInformation("OpenTelemetry {Greeting} {Subject}!", "Hello", "World");

            var item = exportedItems[0];
            Assert.Equal("OpenTelemetry Good Night!", item.FormattedMessage);
        }

        [Fact]
        public void CheckTraceIdForLogWithinDroppedActivity()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            logger.LogInformation("Log within a dropped activity");
            var logRecord = exportedItems[0];

            Assert.Null(Activity.Current);
            Assert.Equal(default, logRecord.TraceId);
            Assert.Equal(default, logRecord.SpanId);
            Assert.Equal(default, logRecord.TraceFlags);
        }

        [Fact]
        public void CheckTraceIdForLogWithinActivityMarkedAsRecordOnly()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

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

            logger.LogInformation("Log within activity marked as RecordOnly");
            var logRecord = exportedItems[0];

            var currentActivity = Activity.Current;
            Assert.NotNull(Activity.Current);
            Assert.Equal(currentActivity.TraceId, logRecord.TraceId);
            Assert.Equal(currentActivity.SpanId, logRecord.SpanId);
            Assert.Equal(currentActivity.ActivityTraceFlags, logRecord.TraceFlags);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CheckTraceIdForLogWithinActivityMarkedAsRecordAndSample(bool includeTraceState)
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeTraceState = includeTraceState);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

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

            Assert.NotNull(activity);

            activity!.TraceStateString = "key1=value1";

            logger.LogInformation("Log within activity marked as RecordAndSample");
            var logRecord = exportedItems[0];

            var currentActivity = Activity.Current;
            Assert.NotNull(Activity.Current);
            Assert.Equal(currentActivity.TraceId, logRecord.TraceId);
            Assert.Equal(currentActivity.SpanId, logRecord.SpanId);
            Assert.Equal(currentActivity.ActivityTraceFlags, logRecord.TraceFlags);
            if (includeTraceState)
            {
                Assert.Equal(currentActivity.TraceStateString, logRecord.TraceState);
            }
            else
            {
                Assert.Null(logRecord.TraceState);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void VerifyIncludeFormattedMessage(bool includeFormattedMessage)
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeFormattedMessage = includeFormattedMessage);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            logger.LogInformation("OpenTelemetry!");
            var logRecord = exportedItems[0];

            if (includeFormattedMessage)
            {
                Assert.Equal("OpenTelemetry!", logRecord.FormattedMessage);
            }
            else
            {
                Assert.Null(logRecord.FormattedMessage);
            }

            logger.LogInformation("OpenTelemetry {Greeting} {Subject}!", "Hello", "World");
            logRecord = exportedItems[1];

            if (includeFormattedMessage)
            {
                Assert.Equal("OpenTelemetry Hello World!", logRecord.FormattedMessage);
            }
            else
            {
                Assert.Null(logRecord.FormattedMessage);
            }
        }

        [Fact]
        public void IncludeFormattedMessageTestWhenFormatterNull()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeFormattedMessage = true);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            logger.Log(LogLevel.Information, default, "Hello World!", null, null);
            var logRecord = exportedItems[0];
            Assert.Null(logRecord.FormattedMessage);

            // Pass null as formatter function
            logger.Log(LogLevel.Information, default, "Hello World!", null, null);
            logRecord = exportedItems[1];
            Assert.Null(logRecord.FormattedMessage);

            var expectedFormattedMessage = "formatted message";
            logger.Log(LogLevel.Information, default, "Hello World!", null, (state, ex) => expectedFormattedMessage);
            logRecord = exportedItems[2];
            Assert.Equal(expectedFormattedMessage, logRecord.FormattedMessage);
        }

        [Fact]
        public void VerifyIncludeScopes_False()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeScopes = false);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            using var scope = logger.BeginScope("string_scope");

            logger.LogInformation("OpenTelemetry!");
            var logRecord = exportedItems[0];

            List<object> scopes = new List<object>();
            logRecord.ForEachScope<object>((scope, state) => scopes.Add(scope.Scope), null);
            Assert.Empty(scopes);
        }

        [Fact]
        public void VerifyIncludeScopes_True()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeScopes = true);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            using var scope = logger.BeginScope("string_scope");

            logger.LogInformation("OpenTelemetry!");
            var logRecord = exportedItems[0];

            List<object> scopes = new List<object>();

            logger.LogInformation("OpenTelemetry!");
            logRecord = exportedItems[1];

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
            using var scope2 = logger.BeginScope(expectedScope2);

            logger.LogInformation("OpenTelemetry!");
            logRecord = exportedItems[2];

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
            using var scope3 = logger.BeginScope(expectedScope3);

            logger.LogInformation("OpenTelemetry!");
            logRecord = exportedItems[3];

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void VerifyParseStateValues_UsingStandardExtensions(bool parseStateValues)
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = parseStateValues);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            // Tests state parsing with standard extensions.

            logger.LogInformation("{Product} {Year}!", "OpenTelemetry", 2021);
            var logRecord = exportedItems[0];

            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.StateValues);
            Assert.Equal(3, logRecord.StateValues.Count);
            Assert.Equal(new KeyValuePair<string, object>("Product", "OpenTelemetry"), logRecord.StateValues[0]);
            Assert.Equal(new KeyValuePair<string, object>("Year", 2021), logRecord.StateValues[1]);
            Assert.Equal(new KeyValuePair<string, object>("{OriginalFormat}", "{Product} {Year}!"), logRecord.StateValues[2]);

            Assert.NotNull(logRecord.Body);
            Assert.Null(logRecord.FormattedMessage);
            Assert.Equal("{Product} {Year}!", logRecord.Body);

            var complex = new { Property = "Value" };

            logger.LogInformation("{Product} {Year} {Complex}!", "OpenTelemetry", 2021, complex);
            logRecord = exportedItems[1];

            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.StateValues);
            Assert.Equal(4, logRecord.StateValues.Count);
            Assert.Equal(new KeyValuePair<string, object>("Product", "OpenTelemetry"), logRecord.StateValues[0]);
            Assert.Equal(new KeyValuePair<string, object>("Year", 2021), logRecord.StateValues[1]);
            Assert.Equal(new KeyValuePair<string, object>("{OriginalFormat}", "{Product} {Year} {Complex}!"), logRecord.StateValues[3]);

            KeyValuePair<string, object> actualComplex = logRecord.StateValues[2];
            Assert.Equal("Complex", actualComplex.Key);
            Assert.Same(complex, actualComplex.Value);

            Assert.NotNull(logRecord.Body);
            Assert.Null(logRecord.FormattedMessage);
            Assert.Equal("{Product} {Year} {Complex}!", logRecord.Body);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ParseStateValuesUsingStructTest(bool parseStateValues)
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = parseStateValues);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            // Tests struct IReadOnlyList<KeyValuePair<string, object>> parse path.

            logger.Log(
                LogLevel.Information,
                0,
                new StructState(new KeyValuePair<string, object>("Key1", "Value1")),
                null,
                (s, e) => "OpenTelemetry!");
            var logRecord = exportedItems[0];

            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.StateValues);
            Assert.Equal(1, logRecord.StateValues.Count);
            Assert.Equal(new KeyValuePair<string, object>("Key1", "Value1"), logRecord.StateValues[0]);

            Assert.NotNull(logRecord.Body);
            Assert.NotNull(logRecord.FormattedMessage);
            Assert.Equal("OpenTelemetry!", logRecord.Body);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ParseStateValuesUsingListTest(bool parseStateValues)
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = parseStateValues);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            // Tests ref IReadOnlyList<KeyValuePair<string, object>> parse path.

            logger.Log(
                LogLevel.Information,
                0,
                new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("Key1", "Value1") },
                null,
                (s, e) => "OpenTelemetry!");
            var logRecord = exportedItems[0];

            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.StateValues);
            Assert.Equal(1, logRecord.StateValues.Count);
            Assert.Equal(new KeyValuePair<string, object>("Key1", "Value1"), logRecord.StateValues[0]);

            Assert.NotNull(logRecord.Body);
            Assert.NotNull(logRecord.FormattedMessage);
            Assert.Equal("OpenTelemetry!", logRecord.Body);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ParseStateValuesUsingIEnumerableTest(bool parseStateValues)
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = parseStateValues);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            // Tests IEnumerable<KeyValuePair<string, object>> parse path.

            logger.Log(
                LogLevel.Information,
                0,
                new ListState(new KeyValuePair<string, object>("Key1", "Value1")),
                null,
                (s, e) => "OpenTelemetry!");
            var logRecord = exportedItems[0];

            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.StateValues);
            Assert.Equal(1, logRecord.StateValues.Count);
            Assert.Equal(new KeyValuePair<string, object>("Key1", "Value1"), logRecord.StateValues[0]);

            Assert.NotNull(logRecord.Body);
            Assert.NotNull(logRecord.FormattedMessage);
            Assert.Equal("OpenTelemetry!", logRecord.Body);
        }

        [Fact]
        public void ParseStateValuesWithOriginalFormatSetsBodyTest()
        {
            using var loggerFactory = InitializeLoggerFactory(
                out List<LogRecord> exportedItems,
                configure: options =>
                {
                    options.ParseStateValues = false;
                    options.IncludeFormattedMessage = false;
                });
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            logger.Log(
                LogLevel.Information,
                0,
                new ListState(new KeyValuePair<string, object>("{OriginalFormat}", "Message_Template_Goes_Here")),
                null,
                (s, e) => "OpenTelemetry!");

            var logRecord = exportedItems[0];

            Assert.NotNull(logRecord.Body);
            Assert.Equal("Message_Template_Goes_Here", logRecord.Body);
            Assert.Null(logRecord.FormattedMessage);

            exportedItems.Clear();

            logger.Log(
                LogLevel.Information,
                0,
                new ListState(
                    new KeyValuePair<string, object>("{OriginalFormat}", "Message_Template_Goes_Here"), // <- Ignored because it is not the last item
                    new KeyValuePair<string, object>("Key1", "Value1")),
                null,
                (s, e) => "OpenTelemetry!");

            logRecord = exportedItems[0];

            Assert.Equal("OpenTelemetry!", logRecord.Body);
            Assert.Equal("OpenTelemetry!", logRecord.FormattedMessage);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ParseStateValuesUsingCustomTest(bool parseStateValues)
        {
            using var loggerFactory = InitializeLoggerFactory(
                out List<LogRecord> exportedItems,
                configure: options =>
                {
                    options.ParseStateValues = parseStateValues;
                    options.IncludeFormattedMessage = false;
                });
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            // Tests unknown state parse path.

            CustomState state = new CustomState
            {
                Property1 = "Value1",
                Property2 = "Value2",
            };

            logger.Log(
                LogLevel.Information,
                0,
                state,
                null,
                (s, e) => "OpenTelemetry!");

            var logRecord = exportedItems[0];

            if (parseStateValues)
            {
                Assert.Null(logRecord.State);
                Assert.NotNull(logRecord.Attributes);
                Assert.Equal(2, logRecord.Attributes.Count);

                Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "Property1" && (string)kvp.Value == "Value1");
                Assert.Contains(logRecord.Attributes, kvp => kvp.Key == "Property2" && (string)kvp.Value == "Value2");
            }
            else
            {
                Assert.NotNull(logRecord.State);
                Assert.Null(logRecord.Attributes);
            }

            Assert.NotNull(logRecord.Body);
            Assert.NotNull(logRecord.FormattedMessage);
            Assert.Equal("OpenTelemetry!", logRecord.Body);
        }

        [Fact]
        public void DisposingStateTest()
        {
            using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = true);
            var logger = loggerFactory.CreateLogger<LogRecordTest>();

            DisposingState state = new DisposingState("Hello world");

            logger.Log(
                LogLevel.Information,
                0,
                state,
                null,
                (s, e) => "OpenTelemetry!");
            var logRecord = exportedItems[0];

            state.Dispose();

            Assert.Null(logRecord.State);
            Assert.NotNull(logRecord.StateValues);
            Assert.Equal(1, logRecord.StateValues.Count);

            KeyValuePair<string, object> actualState = logRecord.StateValues[0];

            Assert.Same("Value", actualState.Key);
            Assert.Same("Hello world", actualState.Value);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ReusedLogRecordScopeTest(bool buffer)
        {
            var processor = new ScopeProcessor(buffer);

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    options.IncludeScopes = true;
                    options.AddProcessor(processor);
                });
            });

            var logger = loggerFactory.CreateLogger("TestLogger");

            using (var scope1 = logger.BeginScope("scope1"))
            {
                logger.LogInformation("message1");
            }

            using (var scope2 = logger.BeginScope("scope2"))
            {
                logger.LogInformation("message2");
            }

            Assert.Equal(2, processor.Scopes.Count);
            Assert.Equal("scope1", processor.Scopes[0]);
            Assert.Equal("scope2", processor.Scopes[1]);
        }

        private static ILoggerFactory InitializeLoggerFactory(out List<LogRecord> exportedItems, Action<OpenTelemetryLoggerOptions> configure = null)
        {
            var items = exportedItems = new List<LogRecord>();

            return LoggerFactory.Create(builder =>
            {
                builder.AddOpenTelemetry(options =>
                {
                    configure?.Invoke(options);
                    options.AddInMemoryExporter(items);
                });
                builder.AddFilter(typeof(LogRecordTest).FullName, LogLevel.Trace);
            });
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

        internal sealed class DisposingState : IReadOnlyList<KeyValuePair<string, object>>, IDisposable
        {
            private string value;
            private bool disposed;

            public DisposingState(string value)
            {
                this.Value = value;
            }

            public int Count => 1;

            public string Value
            {
                get
                {
                    if (this.disposed)
                    {
                        throw new ObjectDisposedException(nameof(DisposingState));
                    }

                    return this.value;
                }
                private set => this.value = value;
            }

            public KeyValuePair<string, object> this[int index] => index switch
            {
                0 => new KeyValuePair<string, object>(nameof(this.Value), this.Value),
                _ => throw new IndexOutOfRangeException(nameof(index)),
            };

            public void Dispose()
            {
                this.disposed = true;
            }

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                for (var i = 0; i < this.Count; i++)
                {
                    yield return this[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }

        private class RedactionProcessor : BaseProcessor<LogRecord>
        {
            private readonly Field fieldToUpdate;

            public RedactionProcessor(Field fieldToUpdate)
            {
                this.fieldToUpdate = fieldToUpdate;
            }

            public override void OnEnd(LogRecord logRecord)
            {
                if (this.fieldToUpdate == Field.State)
                {
                    logRecord.State = new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("newStateKey", "newStateValue") };
                }
                else if (this.fieldToUpdate == Field.StateValues)
                {
                    logRecord.StateValues = new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("newStateValueKey", "newStateValueValue") };
                }
                else
                {
                    logRecord.FormattedMessage = "OpenTelemetry Good Night!";
                }
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
            public int Field1 = 18;

            public string Property1 { get; set; }

            public string Property2 { get; set; }

            public string Property3 { get; set; }
        }

        private class ScopeProcessor : BaseProcessor<LogRecord>
        {
            private readonly bool buffer;

            public ScopeProcessor(bool buffer)
            {
                this.buffer = buffer;
            }

            public List<object> Scopes { get; } = new();

            public override void OnEnd(LogRecord data)
            {
                data.ForEachScope<object>(
                    (scope, state) =>
                    {
                        this.Scopes.Add(scope.Scope);
                    },
                    null);

                if (this.buffer)
                {
                    data.Buffer();
                }
            }
        }
    }
}
