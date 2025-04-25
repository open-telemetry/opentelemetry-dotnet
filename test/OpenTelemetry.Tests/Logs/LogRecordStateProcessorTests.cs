// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public class LogRecordStateProcessorTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void LogProcessorSetStateTest(bool includeAttributes, bool parseStateValues)
    {
        List<LogRecord> exportedItems = new();

        using (var loggerFactory = CreateLoggerFactory(includeAttributes, parseStateValues, exportedItems, OnEnd))
        {
            var logger = loggerFactory.CreateLogger("TestLogger");

            logger.LogInformation("Hello world {Data}", 1234);
        }

        Assert.Single(exportedItems);

        AssertStateAndAttributes(
            exportedItems[0],
            attributesExpectedCount: !includeAttributes ? 0 : parseStateValues ? 1 : 3,
            stateExpectedCount: !includeAttributes || parseStateValues ? 1 : 3,
            out var state,
            out var attributes);

        void OnEnd(LogRecord logRecord)
        {
            AssertStateAndAttributes(
                logRecord,
                attributesExpectedCount: includeAttributes ? 2 : 0,
                stateExpectedCount: !includeAttributes || parseStateValues ? 0 : 2,
                out var state,
                out var attributes);

            logRecord.State = new List<KeyValuePair<string, object?>>(state)
            {
                new("enrichedData", "OTel"),
            };
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void LogProcessorSetStateToUnsupportedTypeTest(bool includeAttributes, bool parseStateValues)
    {
        List<LogRecord> exportedItems = new();

        using (var loggerFactory = CreateLoggerFactory(includeAttributes, parseStateValues, exportedItems, OnEnd))
        {
            var logger = loggerFactory.CreateLogger("TestLogger");

            logger.LogInformation("Hello world {Data}", 1234);
        }

        Assert.Single(exportedItems);

        AssertStateAndAttributes(
            exportedItems[0],
            attributesExpectedCount: 0,
            stateExpectedCount: 0,
            out var state,
            out var attributes);

        Assert.True(exportedItems[0].State is CustomState);

        void OnEnd(LogRecord logRecord)
        {
            AssertStateAndAttributes(
                logRecord,
                attributesExpectedCount: includeAttributes ? 2 : 0,
                stateExpectedCount: !includeAttributes || parseStateValues ? 0 : 2,
                out var state,
                out var attributes);

            logRecord.State = new CustomState("OTel");
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void LogProcessorSetAttributesTest(bool includeAttributes, bool parseStateValues)
    {
        List<LogRecord> exportedItems = new();

        using (var loggerFactory = CreateLoggerFactory(includeAttributes, parseStateValues, exportedItems, OnEnd))
        {
            var logger = loggerFactory.CreateLogger("TestLogger");

            logger.LogInformation("Hello world {Data}", 1234);
        }

        Assert.Single(exportedItems);

        AssertStateAndAttributes(
            exportedItems[0],
            attributesExpectedCount: !includeAttributes ? 1 : 3,
            stateExpectedCount: !includeAttributes || parseStateValues ? 0 : 3,
            out var state,
            out var attributes);

        void OnEnd(LogRecord logRecord)
        {
            AssertStateAndAttributes(
                logRecord,
                attributesExpectedCount: includeAttributes ? 2 : 0,
                stateExpectedCount: !includeAttributes || parseStateValues ? 0 : 2,
                out var state,
                out var attributes);

            logRecord.Attributes = new List<KeyValuePair<string, object?>>(attributes)
            {
                new("enrichedData", "OTel"),
            };
        }
    }

    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, true, 1)]
    [InlineData(true, true, 1)]
    [InlineData(false, false, 1)]
    public void LogProcessorSetAttributesAndStateMixedTest(bool includeAttributes, bool parseStateValues, int order)
    {
        List<LogRecord> exportedItems = new();

        using (var loggerFactory = CreateLoggerFactory(includeAttributes, parseStateValues, exportedItems, OnEnd))
        {
            var logger = loggerFactory.CreateLogger("TestLogger");

            logger.LogInformation("Hello world {Data}", 1234);
        }

        Assert.Single(exportedItems);

        AssertStateAndAttributes(
            exportedItems[0],
            attributesExpectedCount: !includeAttributes ? 1 : 3,
            stateExpectedCount: !includeAttributes ? 1 : 3,
            out var state,
            out var attributes);

        void OnEnd(LogRecord logRecord)
        {
            AssertStateAndAttributes(
                logRecord,
                attributesExpectedCount: includeAttributes ? 2 : 0,
                stateExpectedCount: !includeAttributes || parseStateValues ? 0 : 2,
                out var state,
                out var attributes);

            if (order == 0)
            {
                logRecord.State = logRecord.Attributes = new List<KeyValuePair<string, object?>>(attributes)
                {
                    new("enrichedData", "OTel"),
                };
            }
            else
            {
                var newState = new List<KeyValuePair<string, object?>>(attributes)
                {
                    new("enrichedData", "OTel"),
                };

                logRecord.State = newState;
                logRecord.Attributes = newState;
            }
        }
    }

    private static ILoggerFactory CreateLoggerFactory(
        bool includeAttributes,
        bool parseStateValues,
        List<LogRecord> exportedItems,
        Action<LogRecord> onEndAction)
    {
        return LoggerFactory.Create(logging => logging
            .AddOpenTelemetry(options =>
            {
                options.IncludeAttributes = includeAttributes;
                options.ParseStateValues = parseStateValues;

                options
                    .AddProcessor(new LogRecordStateProcessor(onEndAction))
                    .AddInMemoryExporter(exportedItems);
            }));
    }

    private static void AssertStateAndAttributes(
        LogRecord logRecord,
        int attributesExpectedCount,
        int stateExpectedCount,
        [NotNull] out IReadOnlyList<KeyValuePair<string, object?>>? state,
        [NotNull] out IReadOnlyList<KeyValuePair<string, object?>>? attributes)
    {
        state = logRecord.State as IReadOnlyList<KeyValuePair<string, object?>>;
        attributes = logRecord.Attributes;

        if (stateExpectedCount > 0)
        {
            Assert.NotNull(state);
            Assert.Equal(stateExpectedCount, state.Count);
        }
        else
        {
            Assert.Null(state);
            state = Array.Empty<KeyValuePair<string, object?>>();
        }

        if (attributesExpectedCount > 0)
        {
            Assert.NotNull(attributes);
            Assert.Equal(attributesExpectedCount, attributes.Count);
        }
        else
        {
            Assert.Null(attributes);
            attributes = Array.Empty<KeyValuePair<string, object?>>();
        }
    }

    private sealed class LogRecordStateProcessor : BaseProcessor<LogRecord>
    {
        private readonly Action<LogRecord> onEndAction;

        public LogRecordStateProcessor(Action<LogRecord> onEndAction)
        {
            this.onEndAction = onEndAction;
        }

        public override void OnEnd(LogRecord data)
        {
            this.onEndAction(data);

            base.OnEnd(data);
        }
    }

    private sealed class CustomState(string enrichedData)
    {
        public string EnrichedData { get; } = enrichedData;
    }
}
