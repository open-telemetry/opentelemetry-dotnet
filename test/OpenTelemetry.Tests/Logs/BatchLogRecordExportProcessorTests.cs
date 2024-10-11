// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class BatchLogRecordExportProcessorTests
{
    [Fact]
    public void StateValuesAndScopeBufferingTest()
    {
        var scopeProvider = new LoggerExternalScopeProvider();

        List<LogRecord> exportedItems = new();

        using var processor = new BatchLogRecordExportProcessor(
            new InMemoryExporter<LogRecord>(exportedItems),
            scheduledDelayMilliseconds: int.MaxValue);

        using var scope = scopeProvider.Push(exportedItems);

        var pool = LogRecordSharedPool.Current;

        var logRecord = pool.Rent();

        var state = new LogRecordTest.DisposingState("Hello world");

        logRecord.ILoggerData.ScopeProvider = scopeProvider;
        logRecord.StateValues = state;

        processor.OnEnd(logRecord);

        state.Dispose();

        Assert.Empty(exportedItems);

        Assert.Null(logRecord.ILoggerData.ScopeProvider);
        Assert.False(ReferenceEquals(state, logRecord.StateValues));
        Assert.NotNull(logRecord.AttributeStorage);
        Assert.NotNull(logRecord.ILoggerData.BufferedScopes);

        KeyValuePair<string, object?> actualState = logRecord.StateValues[0];

        Assert.Same("Value", actualState.Key);
        Assert.Same("Hello world", actualState.Value);

        int scopeCount = 0;
        bool foundScope = false;

        logRecord.ForEachScope<object?>(
            (s, o) =>
            {
                foundScope = ReferenceEquals(s.Scope, exportedItems);
                scopeCount++;
            },
            null);

        Assert.Equal(1, scopeCount);
        Assert.True(foundScope);

        processor.Shutdown();

        Assert.Single(exportedItems);
        Assert.Same(logRecord, exportedItems[0]);
    }

    [Fact]
    public void StateBufferingTest()
    {
        // LogRecord.State is never inspected or buffered. Accessing it
        // after OnEnd may throw. This test verifies that behavior. TODO:
        // Investigate this. Potentially obsolete logRecord.State and force
        // StateValues/ParseStateValues behavior.
        List<LogRecord> exportedItems = new();

        using var processor = new BatchLogRecordExportProcessor(
            new InMemoryExporter<LogRecord>(exportedItems));

        var pool = LogRecordSharedPool.Current;

        var logRecord = pool.Rent();

        var state = new LogRecordTest.DisposingState("Hello world");
        logRecord.State = state;

        processor.OnEnd(logRecord);
        processor.Shutdown();

        Assert.Single(exportedItems);
        Assert.Same(logRecord, exportedItems[0]);

        state.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            IReadOnlyList<KeyValuePair<string, object>> state = (IReadOnlyList<KeyValuePair<string, object>>)logRecord.State;

            foreach (var kvp in state)
            {
            }
        });
    }

    [Fact]
    public void CopyMadeWhenLogRecordIsFromThreadStaticPoolTest()
    {
        List<LogRecord> exportedItems = new();

        using var processor = new BatchLogRecordExportProcessor(
            new InMemoryExporter<LogRecord>(exportedItems));

        var pool = LogRecordThreadStaticPool.Instance;

        var logRecord = pool.Rent();

        processor.OnEnd(logRecord);
        processor.Shutdown();

        Assert.Single(exportedItems);
        Assert.NotSame(logRecord, exportedItems[0]);
    }

    [Fact]
    public void LogRecordAddedToBatchIfNotFromAnyPoolTest()
    {
        List<LogRecord> exportedItems = new();

        using var processor = new BatchLogRecordExportProcessor(
            new InMemoryExporter<LogRecord>(exportedItems));

        var logRecord = new LogRecord();

        processor.OnEnd(logRecord);
        processor.Shutdown();

        Assert.Single(exportedItems);
        Assert.Same(logRecord, exportedItems[0]);
    }
}
#endif
