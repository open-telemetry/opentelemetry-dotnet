// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class LogRecordTests
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
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.Log();
        var categoryName = exportedItems[0].CategoryName;

        Assert.Equal(typeof(LogRecordTests).FullName, categoryName);
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
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.Log(logLevel);

        var logLevelRecorded = exportedItems[0].LogLevel;
        Assert.Equal(logLevel, logLevelRecorded);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckStateForUnstructuredLog(bool includeFormattedMessage)
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeFormattedMessage = includeFormattedMessage);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.HelloWorld();

        Assert.NotNull(exportedItems[0].State);

        var attributes = exportedItems[0].Attributes;
        Assert.NotNull(attributes);

        // state only has {OriginalFormat}
        Assert.Single(attributes);

        Assert.Equal("Hello, World!", exportedItems[0].Body);
        if (includeFormattedMessage)
        {
            Assert.Equal("Hello, World!", exportedItems[0].FormattedMessage);
        }
        else
        {
            Assert.Null(exportedItems[0].FormattedMessage);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckStateForUnstructuredLogWithStringInterpolation(bool includeFormattedMessage)
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeFormattedMessage = includeFormattedMessage);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

#pragma warning disable CA2254 // Template should be a static expression
#pragma warning disable CA1848 // Use the LoggerMessage delegates
        var message = $"Hello from potato {0.99}.";
        logger.LogInformation(message);
#pragma warning restore CA1848 // Use the LoggerMessage delegates
#pragma warning restore CA2254 // Template should be a static expression

        Assert.NotNull(exportedItems[0].State);

        var attributes = exportedItems[0].Attributes;
        Assert.NotNull(attributes);

        // state only has {OriginalFormat}
        Assert.Single(attributes);

        Assert.Equal(message, exportedItems[0].Body);
        if (includeFormattedMessage)
        {
            Assert.Equal(message, exportedItems[0].FormattedMessage);
        }
        else
        {
            Assert.Null(exportedItems[0].FormattedMessage);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckStateForStructuredLogWithTemplate(bool includeFormattedMessage)
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeFormattedMessage = includeFormattedMessage);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.HelloFrom("tomato", 2.99);

        Assert.NotNull(exportedItems[0].State);

        var attributes = exportedItems[0].Attributes;
        Assert.NotNull(attributes);

        // state has name, price and {OriginalFormat}
        Assert.Equal(3, attributes.Count);

        // Check if state has name
        Assert.Contains(attributes, item => item.Key == "Name");
        Assert.Equal("tomato", attributes.First(item => item.Key == "Name").Value);

        // Check if state has price
        Assert.Contains(attributes, item => item.Key == "Price");
        Assert.Equal(2.99, attributes.First(item => item.Key == "Price").Value);

        // Check if state has OriginalFormat
        Assert.Contains(attributes, item => item.Key == "{OriginalFormat}");
        Assert.Equal("Hello from {Name} {Price}.", attributes.First(item => item.Key == "{OriginalFormat}").Value);

        Assert.Equal("Hello from {Name} {Price}.", exportedItems[0].Body);
        if (includeFormattedMessage)
        {
            Assert.Equal("Hello from tomato 2.99.", exportedItems[0].FormattedMessage);
        }
        else
        {
            Assert.Null(exportedItems[0].FormattedMessage);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckStateForStructuredLogWithStrongType(bool includeFormattedMessage)
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeFormattedMessage = includeFormattedMessage);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        var food = new Food { Name = "artichoke", Price = 3.99 };
        logger.Food(food);

        Assert.NotNull(exportedItems[0].State);

        var attributes = exportedItems[0].Attributes;
        Assert.NotNull(attributes);

        // state has food and {OriginalFormat}
        Assert.Equal(2, attributes.Count);

        // Check if state has food
        Assert.Contains(attributes, item => item.Key == "Food");

        var foodParameter = attributes.First(item => item.Key == "Food").Value as Food?;
        Assert.NotNull(foodParameter);
        Assert.Equal(food.Name, foodParameter.Value.Name);
        Assert.Equal(food.Price, foodParameter.Value.Price);

        // Check if state has OriginalFormat
        Assert.Contains(attributes, item => item.Key == "{OriginalFormat}");
        Assert.Equal("{Food}", attributes.First(item => item.Key == "{OriginalFormat}").Value);

        Assert.Equal("{Food}", exportedItems[0].Body);
        if (includeFormattedMessage)
        {
            Assert.Equal(food.ToString(), exportedItems[0].FormattedMessage);
        }
        else
        {
            Assert.Null(exportedItems[0].FormattedMessage);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckStateForStructuredLogWithAnonymousType(bool includeFormattedMessage)
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeFormattedMessage = includeFormattedMessage);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        var anonymousType = new { Name = "pumpkin", Price = 5.99 };
        logger.Food(anonymousType);

        Assert.NotNull(exportedItems[0].State);

        var attributes = exportedItems[0].Attributes;
        Assert.NotNull(attributes);

        // state has food and {OriginalFormat}
        Assert.Equal(2, attributes.Count);

        // Check if state has food
        Assert.Contains(attributes, item => item.Key == "Food");

        var foodParameter = attributes.First(item => item.Key == "Food").Value as dynamic;
        Assert.NotNull(foodParameter);
        Assert.Equal(anonymousType.Name, foodParameter!.Name);
        Assert.Equal(anonymousType.Price, foodParameter!.Price);

        // Check if state has OriginalFormat
        Assert.Contains(attributes, item => item.Key == "{OriginalFormat}");
        Assert.Equal("{Food}", attributes.First(item => item.Key == "{OriginalFormat}").Value);

        Assert.Equal("{Food}", exportedItems[0].Body);
        if (includeFormattedMessage)
        {
            Assert.Equal(anonymousType.ToString(), exportedItems[0].FormattedMessage);
        }
        else
        {
            Assert.Null(exportedItems[0].FormattedMessage);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckStateForStructuredLogWithGeneralType(bool includeFormattedMessage)
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeFormattedMessage = includeFormattedMessage);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();
        var trufflePrice = 299.99;

        var food = new Dictionary<string, object>
        {
            ["Name"] = "truffle",
            ["Price"] = trufflePrice,
        };
        logger.Food(food);

        Assert.NotNull(exportedItems[0].State);

        var attributes = exportedItems[0].Attributes;
        Assert.NotNull(attributes);

        // state only has food and {OriginalFormat}
        Assert.Equal(2, attributes.Count);

        // Check if state has food
        Assert.Contains(attributes, item => item.Key == "Food");

        var foodParameter = attributes.First(item => item.Key == "Food").Value as Dictionary<string, object>;
        Assert.NotNull(foodParameter);
        Assert.True(food.Count == foodParameter.Count && !food.Except(foodParameter).Any());

        // Check if state has OriginalFormat
        Assert.Contains(attributes, item => item.Key == "{OriginalFormat}");
        Assert.Equal("{Food}", attributes.First(item => item.Key == "{OriginalFormat}").Value);

        Assert.Equal("{Food}", exportedItems[0].Body);
        if (includeFormattedMessage)
        {
            var priceInCurrentCulture = trufflePrice.ToString(CultureInfo.CurrentCulture);
            Assert.Equal($"[Name, truffle], [Price, {priceInCurrentCulture}]", exportedItems[0].FormattedMessage);
        }
        else
        {
            Assert.Null(exportedItems[0].FormattedMessage);
        }
    }

    [Fact]
    public void CheckStateForExceptionLogged()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        var exceptionMessage = "Exception Message";
        var exception = new InvalidOperationException(exceptionMessage);

        logger.LogException(exception);

        Assert.NotNull(exportedItems[0].State);

        var state = exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object?>>;
        Assert.NotNull(state);
        var itemCount = state.Count;

        // state only has {OriginalFormat}
        Assert.Equal(1, itemCount);

        var attributes = exportedItems[0].Attributes;
        Assert.NotNull(attributes);

        // state only has {OriginalFormat}
        Assert.Single(attributes);

        var loggedException = exportedItems[0].Exception;
        Assert.NotNull(loggedException);
        Assert.Equal(exceptionMessage, loggedException.Message);

        Assert.Equal("Exception Occurred", exportedItems[0].Body);
        Assert.Equal("Exception Occurred", state.ToString());
        Assert.Null(exportedItems[0].FormattedMessage);
    }

    [Fact]
    public void CheckStateCanBeSet()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.Log();

        var logRecord = exportedItems[0];
        logRecord.State = "newState";

        var expectedState = "newState";
        Assert.Equal(expectedState, logRecord.State);
    }

    [Fact]
    public void CheckStateValuesCanBeSet()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.Log(
            LogLevel.Information,
            0,
            new List<KeyValuePair<string, object?>> { new("Key1", "Value1") },
            null,
            (s, e) => "OpenTelemetry!");

        var logRecord = exportedItems[0];
        var expectedStateValues = new List<KeyValuePair<string, object?>> { new("Key2", "Value2") };
        logRecord.StateValues = expectedStateValues;

        Assert.Equal(expectedStateValues, logRecord.StateValues);
    }

    [Fact]
    public void CheckFormattedMessageCanBeSet()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeFormattedMessage = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.HelloFrom("tomato", 3.0);
        var logRecord = exportedItems[0];
        var expectedFormattedMessage = "OpenTelemetry Good Night!";
        logRecord.FormattedMessage = expectedFormattedMessage;

        Assert.Equal(expectedFormattedMessage, logRecord.FormattedMessage);
    }

    [Fact]
    public void CheckStateCanBeSetByProcessor()
    {
        var exportedItems = new List<LogRecord>();
#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new InMemoryExporter<LogRecord>(exportedItems);
#pragma warning restore CA2000 // Dispose objects before losing scope
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new RedactionProcessor(Field.State));
                options.AddInMemoryExporter(exportedItems);
            });
        });

        var logger = loggerFactory.CreateLogger<LogRecordTests>();
        logger.Log();

        var state = exportedItems[0].State as IReadOnlyList<KeyValuePair<string, object>>;
        Assert.NotNull(state);
        Assert.Equal("newStateKey", state[0].Key.ToString());
        Assert.Equal("newStateValue", state[0].Value.ToString());
    }

    [Fact]
    public void CheckStateValuesCanBeSetByProcessor()
    {
        var exportedItems = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new RedactionProcessor(Field.StateValues));
                options.AddInMemoryExporter(exportedItems);
                options.ParseStateValues = true;
            });
        });

        var logger = loggerFactory.CreateLogger<LogRecordTests>();
        logger.Log();

        var stateValue = exportedItems[0];
        Assert.NotNull(stateValue.StateValues);
        Assert.NotEmpty(stateValue.StateValues);
        Assert.Equal(new KeyValuePair<string, object?>("newStateValueKey", "newStateValueValue"), stateValue.StateValues[0]);
    }

    [Fact]
    public void CheckFormattedMessageCanBeSetByProcessor()
    {
        var exportedItems = new List<LogRecord>();
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new RedactionProcessor(Field.FormattedMessage));
                options.AddInMemoryExporter(exportedItems);
                options.IncludeFormattedMessage = true;
            });
        });

        var logger = loggerFactory.CreateLogger<LogRecordTests>();
        logger.HelloFrom("potato", 2.99);

        var item = exportedItems[0];
        Assert.Equal("OpenTelemetry Good Night!", item.FormattedMessage);
    }

    [Fact]
    public void CheckTraceIdForLogWithinDroppedActivity()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.LogWithinADroppedActivity();
        var logRecord = exportedItems[0];

        Assert.Null(Activity.Current);
        Assert.Equal(default, logRecord.TraceId);
        Assert.Equal(default, logRecord.SpanId);
        Assert.Equal(default, logRecord.TraceFlags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CheckTraceIdForLogWithinActivityMarkedAsRecordOnly(bool includeTraceState)
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: o => o.IncludeTraceState = includeTraceState);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        var sampler = new RecordOnlySampler();
        var exportedActivityList = new List<Activity>();
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .AddInMemoryExporter(exportedActivityList)
            .Build();

        using var activity = activitySource.StartActivity("Activity");
        Assert.NotNull(activity);
        activity.TraceStateString = "key1=value1";

        logger.LogWithinRecordOnlyActivity();
        var logRecord = exportedItems[0];

        var currentActivity = Activity.Current;
        Assert.NotNull(currentActivity);
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

    [Fact]
    public void CheckTraceIdForLogWithinActivityMarkedAsRecordAndSample()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: null);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        var sampler = new AlwaysOnSampler();
        var exportedActivityList = new List<Activity>();
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .AddInMemoryExporter(exportedActivityList)
            .Build();

        using var activity = activitySource.StartActivity("Activity");

        logger.LogWithinRecordAndSampleActivity();
        var logRecord = exportedItems[0];

        var currentActivity = Activity.Current;
        Assert.NotNull(currentActivity);
        Assert.Equal(currentActivity.TraceId, logRecord.TraceId);
        Assert.Equal(currentActivity.SpanId, logRecord.SpanId);
        Assert.Equal(currentActivity.ActivityTraceFlags, logRecord.TraceFlags);
    }

    [Fact]
    public void VerifyIncludeFormattedMessage_False()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeFormattedMessage = false);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.Log();
        var logRecord = exportedItems[0];
        Assert.Null(logRecord.FormattedMessage);
    }

    [Fact]
    public void VerifyIncludeFormattedMessage_True()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeFormattedMessage = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.Log();
        var logRecord = exportedItems[0];
        Assert.Equal("Log", logRecord.FormattedMessage);

        logger.HelloFrom("tomato", 3.11);
        logRecord = exportedItems[1];
        Assert.Equal("Hello from tomato 3.11.", logRecord.FormattedMessage);
    }

    [Fact]
    public void IncludeFormattedMessageTestWhenFormatterNull()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeFormattedMessage = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.Log(LogLevel.Information, default, "Hello World!", null, null!);
        var logRecord = exportedItems[0];
        Assert.Equal("Hello World!", logRecord.FormattedMessage);
        Assert.Equal("Hello World!", logRecord.Body);

        logger.Log(LogLevel.Information, default, new CustomState(), null, null!);
        logRecord = exportedItems[1];
        Assert.Equal(CustomState.ToStringValue, logRecord.FormattedMessage);
        Assert.Equal(CustomState.ToStringValue, logRecord.Body);

        var expectedFormattedMessage = "formatted message";
        logger.Log(LogLevel.Information, default, "Hello World!", null, (state, ex) => expectedFormattedMessage);
        logRecord = exportedItems[2];
        Assert.Equal(expectedFormattedMessage, logRecord.FormattedMessage);
        Assert.Equal(expectedFormattedMessage, logRecord.Body);
    }

    [Fact]
    public void VerifyIncludeScopes_False()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeScopes = false);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        using var scope = logger.BeginScope("string_scope");

        logger.Log();
        var logRecord = exportedItems[0];

        List<object?> scopes = [];
        logRecord.ForEachScope<object?>((scope, state) => scopes.Add(scope.Scope), null);
        Assert.Empty(scopes);
    }

    [Fact]
    public void VerifyIncludeScopes_True()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.IncludeScopes = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        using var scope = logger.BeginScope("string_scope");

        logger.Log();
        var logRecord = exportedItems[0];

        List<object?> scopes = [];

        logger.Log();
        logRecord = exportedItems[1];

        int reachedDepth = -1;
        logRecord.ForEachScope<object?>(
            (scope, state) =>
            {
                reachedDepth++;
                scopes.Add(scope.Scope);
                foreach (KeyValuePair<string, object?> item in scope)
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

        List<KeyValuePair<string, object?>> expectedScope2 =
        [
            new KeyValuePair<string, object?>("item1", "value1"),
            new KeyValuePair<string, object?>("item2", "value2"),
        ];
        using var scope2 = logger.BeginScope(expectedScope2);

        logger.Log();
        logRecord = exportedItems[2];

        reachedDepth = -1;
        logRecord.ForEachScope<object?>(
            (scope, state) =>
            {
                scopes.Add(scope.Scope);
                if (reachedDepth++ == 1)
                {
                    foreach (KeyValuePair<string, object?> item in scope)
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

        KeyValuePair<string, object?>[] expectedScope3 =
        [
            new KeyValuePair<string, object?>("item3", "value3"),
            new KeyValuePair<string, object?>("item4", "value4"),
        ];
        using var scope3 = logger.BeginScope(expectedScope3);

        logger.Log();
        logRecord = exportedItems[3];

        reachedDepth = -1;
        logRecord.ForEachScope<object?>(
            (scope, state) =>
            {
                scopes.Add(scope.Scope);
                if (reachedDepth++ == 2)
                {
                    foreach (KeyValuePair<string, object?> item in scope)
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
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        // Tests state parsing with standard extensions.

        logger.LogProduct("OpenTelemetry", 2021);
        var logRecord = exportedItems[0];

        Assert.NotNull(logRecord.StateValues);
        if (parseStateValues)
        {
            Assert.Null(logRecord.State);
        }
        else
        {
            Assert.NotNull(logRecord.State);
        }

        Assert.NotNull(logRecord.StateValues);
        Assert.Equal(3, logRecord.StateValues.Count);
        Assert.Equal(new KeyValuePair<string, object?>("Product", "OpenTelemetry"), logRecord.StateValues[0]);
        Assert.Equal(new KeyValuePair<string, object?>("Year", 2021), logRecord.StateValues[1]);
        Assert.Equal(new KeyValuePair<string, object?>("{OriginalFormat}", "{Product} {Year}!"), logRecord.StateValues[2]);

        var complex = new { Property = "Value" };

        logger.LogProduct("OpenTelemetry", 2021, complex);
        logRecord = exportedItems[1];

        Assert.NotNull(logRecord.StateValues);
        if (parseStateValues)
        {
            Assert.Null(logRecord.State);
        }
        else
        {
            Assert.NotNull(logRecord.State);
        }

        Assert.NotNull(logRecord.StateValues);
        Assert.Equal(4, logRecord.StateValues.Count);
        Assert.Equal(new KeyValuePair<string, object?>("Product", "OpenTelemetry"), logRecord.StateValues[0]);
        Assert.Equal(new KeyValuePair<string, object?>("Year", 2021), logRecord.StateValues[1]);
        Assert.Equal(new KeyValuePair<string, object?>("{OriginalFormat}", "{Product} {Year} {Complex}!"), logRecord.StateValues[3]);

        KeyValuePair<string, object?> actualComplex = logRecord.StateValues[2];
        Assert.Equal("Complex", actualComplex.Key);
        Assert.Same(complex, actualComplex.Value);
    }

    [Fact]
    public void ParseStateValuesUsingStructTest()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

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
        Assert.Single(logRecord.StateValues);
        Assert.Equal(new KeyValuePair<string, object?>("Key1", "Value1"), logRecord.StateValues[0]);
    }

    [Fact]
    public void ParseStateValuesUsingListTest()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        // Tests ref IReadOnlyList<KeyValuePair<string, object>> parse path.

        logger.Log(
            LogLevel.Information,
            0,
            new List<KeyValuePair<string, object>> { new("Key1", "Value1") },
            null,
            (s, e) => "OpenTelemetry!");
        var logRecord = exportedItems[0];

        Assert.Null(logRecord.State);
        Assert.NotNull(logRecord.StateValues);
        Assert.Single(logRecord.StateValues);
        Assert.Equal(new KeyValuePair<string, object?>("Key1", "Value1"), logRecord.StateValues[0]);
    }

    [Fact]
    public void ParseStateValuesUsingIEnumerableTest()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

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
        Assert.Single(logRecord.StateValues);
        Assert.Equal(new KeyValuePair<string, object?>("Key1", "Value1"), logRecord.StateValues[0]);
    }

    [Fact]
    public void ParseStateValuesUsingNonconformingCustomTypeTest()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        // Tests unknown state parse path.

        CustomState state = new CustomState
        {
            Property = "Value",
        };

        logger.Log(
            LogLevel.Information,
            0,
            state,
            null,
            (s, e) => "OpenTelemetry!");
        var logRecord = exportedItems[0];

        Assert.Null(logRecord.State);
        Assert.NotNull(logRecord.StateValues);

        // Note: We currently do not support parsing custom states which do
        // not implement the standard interfaces. We return empty attributes
        // for these.
        Assert.Empty(logRecord.StateValues);
    }

    [Fact]
    public void DisposingStateTest()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems, configure: options => options.ParseStateValues = true);
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

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
        Assert.Single(logRecord.StateValues);

        KeyValuePair<string, object?> actualState = logRecord.StateValues[0];

        Assert.Same("Value", actualState.Key);
        Assert.Same("Hello world", actualState.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReusedLogRecordScopeTest(bool buffer)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var processor = new ScopeProcessor(buffer);
#pragma warning restore CA2000 // Dispose objects before losing scope

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
            logger.Log();
        }

        using (var scope2 = logger.BeginScope("scope2"))
        {
            logger.HelloWorld();
        }

        Assert.Equal(2, processor.Scopes.Count);
        Assert.Equal("scope1", processor.Scopes[0]);
        Assert.Equal("scope2", processor.Scopes[1]);
    }

    [Fact]
    public void IncludeStateTest()
    {
        using var loggerFactory = InitializeLoggerFactory(
            out List<LogRecord> exportedItems, configure: options =>
            {
                options.IncludeAttributes = false;
            });
        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.HelloWorld("Earth");

        var logRecord = exportedItems[0];

        Assert.Null(logRecord.State);
        Assert.Null(logRecord.Attributes);

        Assert.Equal("Hello world Earth", logRecord.Body);
    }

    [Theory]
    [InlineData((int)LogRecordSeverity.Unspecified, LogLevel.Trace)]
    [InlineData(int.MaxValue, LogLevel.Trace)]
    [InlineData((int)LogRecordSeverity.Trace, LogLevel.Trace, (int)LogRecordSeverity.Trace)]
    [InlineData((int)LogRecordSeverity.Trace2, LogLevel.Trace, (int)LogRecordSeverity.Trace)]
    [InlineData((int)LogRecordSeverity.Trace3, LogLevel.Trace, (int)LogRecordSeverity.Trace)]
    [InlineData((int)LogRecordSeverity.Trace4, LogLevel.Trace, (int)LogRecordSeverity.Trace)]
    [InlineData((int)LogRecordSeverity.Debug, LogLevel.Debug, (int)LogRecordSeverity.Debug)]
    [InlineData((int)LogRecordSeverity.Debug2, LogLevel.Debug, (int)LogRecordSeverity.Debug)]
    [InlineData((int)LogRecordSeverity.Debug3, LogLevel.Debug, (int)LogRecordSeverity.Debug)]
    [InlineData((int)LogRecordSeverity.Debug4, LogLevel.Debug, (int)LogRecordSeverity.Debug)]
    [InlineData((int)LogRecordSeverity.Info, LogLevel.Information, (int)LogRecordSeverity.Info)]
    [InlineData((int)LogRecordSeverity.Info2, LogLevel.Information, (int)LogRecordSeverity.Info)]
    [InlineData((int)LogRecordSeverity.Info3, LogLevel.Information, (int)LogRecordSeverity.Info)]
    [InlineData((int)LogRecordSeverity.Info4, LogLevel.Information, (int)LogRecordSeverity.Info)]
    [InlineData((int)LogRecordSeverity.Warn, LogLevel.Warning, (int)LogRecordSeverity.Warn)]
    [InlineData((int)LogRecordSeverity.Warn2, LogLevel.Warning, (int)LogRecordSeverity.Warn)]
    [InlineData((int)LogRecordSeverity.Warn3, LogLevel.Warning, (int)LogRecordSeverity.Warn)]
    [InlineData((int)LogRecordSeverity.Warn4, LogLevel.Warning, (int)LogRecordSeverity.Warn)]
    [InlineData((int)LogRecordSeverity.Error, LogLevel.Error, (int)LogRecordSeverity.Error)]
    [InlineData((int)LogRecordSeverity.Error2, LogLevel.Error, (int)LogRecordSeverity.Error)]
    [InlineData((int)LogRecordSeverity.Error3, LogLevel.Error, (int)LogRecordSeverity.Error)]
    [InlineData((int)LogRecordSeverity.Error4, LogLevel.Error, (int)LogRecordSeverity.Error)]
    [InlineData((int)LogRecordSeverity.Fatal, LogLevel.Critical, (int)LogRecordSeverity.Fatal)]
    [InlineData((int)LogRecordSeverity.Fatal2, LogLevel.Critical, (int)LogRecordSeverity.Fatal)]
    [InlineData((int)LogRecordSeverity.Fatal3, LogLevel.Critical, (int)LogRecordSeverity.Fatal)]
    [InlineData((int)LogRecordSeverity.Fatal4, LogLevel.Critical, (int)LogRecordSeverity.Fatal)]
    public void SeverityLogLevelTest(int logSeverity, LogLevel logLevel, int? transformedLogSeverity = null)
    {
        var severity = (LogRecordSeverity)logSeverity;

        var logRecord = new LogRecord
        {
            Severity = severity,
        };

        Assert.Equal(logLevel, logRecord.LogLevel);

        if (transformedLogSeverity.HasValue)
        {
            logRecord.LogLevel = logLevel;

            Assert.Equal((LogRecordSeverity)transformedLogSeverity.Value, logRecord.Severity);
            Assert.Equal(logLevel.ToString(), logRecord.SeverityText);
        }
    }

    [Fact]
    public void LogRecordInstrumentationScopeTest()
    {
        using var loggerFactory = InitializeLoggerFactory(out List<LogRecord> exportedItems);

        var logger = loggerFactory.CreateLogger<LogRecordTests>();

        logger.HelloWorld();

        var logRecord = exportedItems.FirstOrDefault();

        Assert.NotNull(logRecord);
        Assert.NotNull(logRecord.Logger);
        Assert.Equal("OpenTelemetry.Logs.Tests.LogRecordTests", logRecord.Logger.Name);
        Assert.Null(logRecord.Logger.Version);
    }

    [Fact]
    public void LogRecordCategoryNameAliasForInstrumentationScopeTests()
    {
        LogRecord logRecord = new();

        Assert.Equal(string.Empty, logRecord.CategoryName);
        Assert.Equal(logRecord.CategoryName, logRecord.Logger.Name);

        logRecord.CategoryName = "Testing";

        Assert.Equal("Testing", logRecord.CategoryName);
        Assert.Equal(logRecord.CategoryName, logRecord.Logger.Name);

        logRecord.CategoryName = null;

        Assert.Equal(string.Empty, logRecord.CategoryName);
        Assert.Equal(logRecord.CategoryName, logRecord.Logger.Name);

        var exportedItems = new List<LogRecord>();
        using (var loggerProvider = Sdk.CreateLoggerProviderBuilder()
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new BatchLogRecordExportProcessor(new InMemoryExporter<LogRecord>(exportedItems)))
#pragma warning restore CA2000 // Dispose objects before losing scope
            .Build())
        {
            var logger = loggerProvider.GetLogger("TestName");
            logger.EmitLog(default);
        }

        Assert.Single(exportedItems);

        Assert.Equal("TestName", exportedItems[0].CategoryName);
        Assert.Equal(exportedItems[0].CategoryName, exportedItems[0].Logger.Name);
    }

    private static ILoggerFactory InitializeLoggerFactory(out List<LogRecord> exportedItems, Action<OpenTelemetryLoggerOptions>? configure = null)
    {
        var items = exportedItems = new List<LogRecord>();

        return LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                configure?.Invoke(options);
                options.AddInMemoryExporter(items);
            });
            builder.AddFilter(typeof(LogRecordTests).FullName, LogLevel.Trace);
        });
    }

    internal struct Food
    {
        public string Name { get; set; }

        public double Price { get; set; }
    }

    private readonly struct StructState : IReadOnlyList<KeyValuePair<string, object>>
    {
        private readonly List<KeyValuePair<string, object>> list;

        public StructState(params KeyValuePair<string, object>[] items)
        {
            this.list = [.. items];
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

    internal sealed class DisposingState : IReadOnlyList<KeyValuePair<string, object?>>, IDisposable
    {
        private string? value;
        private bool disposed;

        public DisposingState(string? value)
        {
            this.Value = value;
        }

        public int Count => 1;

        public string? Value
        {
            get
            {
#if NET
                ObjectDisposedException.ThrowIf(this.disposed, this);
#else
                if (this.disposed)
                {
                    throw new ObjectDisposedException(nameof(DisposingState));
                }
#endif

                return this.value;
            }
            private set => this.value = value;
        }

        public KeyValuePair<string, object?> this[int index] => index switch
        {
            0 => new KeyValuePair<string, object?>(nameof(this.Value), this.Value),
#pragma warning disable CA2201 // Do not raise reserved exception types
            _ => throw new IndexOutOfRangeException(nameof(index)),
#pragma warning restore CA2201 // Do not raise reserved exception types
        };

        public void Dispose()
        {
            this.disposed = true;
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (var i = 0; i < this.Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    private sealed class RedactionProcessor : BaseProcessor<LogRecord>
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
                logRecord.State = new List<KeyValuePair<string, object?>> { new("newStateKey", "newStateValue") };
            }
            else if (this.fieldToUpdate == Field.StateValues)
            {
                logRecord.StateValues = new List<KeyValuePair<string, object?>> { new("newStateValueKey", "newStateValueValue") };
            }
            else
            {
                logRecord.FormattedMessage = "OpenTelemetry Good Night!";
            }
        }
    }

    private sealed class ListState : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly List<KeyValuePair<string, object>> list;

        public ListState(params KeyValuePair<string, object>[] items)
        {
            this.list = [.. items];
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

    private sealed class CustomState
    {
        public const string ToStringValue = "CustomState.ToString";

        public string? Property { get; set; }

        public override string ToString()
            => ToStringValue;
    }

    private sealed class ScopeProcessor : BaseProcessor<LogRecord>
    {
        private readonly bool buffer;

        public ScopeProcessor(bool buffer)
        {
            this.buffer = buffer;
        }

        public List<object?> Scopes { get; } = new();

        public override void OnEnd(LogRecord data)
        {
            data.ForEachScope<object?>(
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
