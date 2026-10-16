// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NETFRAMEWORK
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Logs.Tests;

public sealed class BatchLogRecordExportProcessorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StateValuesAndScopeBufferingTest(bool threadingDisabled)
    {
        using var threadingOverride = ThreadingHelper.BeginThreadingOverride(threadingDisabled);

        var scopeProvider = new LoggerExternalScopeProvider();

        List<LogRecord> exportedItems = [];

        using var processor = new BatchLogRecordExportProcessor(
#pragma warning disable CA2000 // Dispose objects before losing scope
            new InMemoryExporter<LogRecord>(exportedItems),
#pragma warning restore CA2000 // Dispose objects before losing scope
            maxQueueSize: BatchLogRecordExportProcessor.DefaultMaxQueueSize,
            scheduledDelayMilliseconds: int.MaxValue,
            exporterTimeoutMilliseconds: BatchLogRecordExportProcessor.DefaultExporterTimeoutMilliseconds,
            maxExportBatchSize: BatchLogRecordExportProcessor.DefaultMaxExportBatchSize);

        using var scope = scopeProvider.Push(exportedItems);

        var pool = LogRecordSharedPool.Current;

        var logRecord = pool.Rent();

        var state = new LogRecordTests.DisposingState("Hello world");

        logRecord.ILoggerData.ScopeProvider = scopeProvider;
        logRecord.StateValues = state;

        processor.OnEnd(logRecord);

        state.Dispose();

        Assert.Empty(exportedItems);

        Assert.Null(logRecord.ILoggerData.ScopeProvider);
        Assert.False(ReferenceEquals(state, logRecord.StateValues));
        Assert.NotNull(logRecord.AttributeStorage);
        Assert.NotNull(logRecord.ILoggerData.BufferedScopes);

        var actualState = logRecord.StateValues[0];

        Assert.Same("Value", actualState.Key);
        Assert.Same("Hello world", actualState.Value);

        var scopeCount = 0;
        var foundScope = false;

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

        var item = Assert.Single(exportedItems);
        Assert.Same(logRecord, item);
    }

    [Fact]
    public void StateBufferingTest()
    {
        // LogRecord.State is never inspected or buffered. Accessing it
        // after OnEnd may throw. This test verifies that behavior. TODO:
        // Investigate this. Potentially obsolete logRecord.State and force
        // StateValues/ParseStateValues behavior.
        List<LogRecord> exportedItems = [];

        using var processor = new BatchLogRecordExportProcessor(
#pragma warning disable CA2000 // Dispose objects before losing scope
            new InMemoryExporter<LogRecord>(exportedItems));
#pragma warning restore CA2000 // Dispose objects before losing scope

        var pool = LogRecordSharedPool.Current;

        var logRecord = pool.Rent();

        var state = new LogRecordTests.DisposingState("Hello world");
        logRecord.State = state;

        processor.OnEnd(logRecord);
        processor.Shutdown();

        var item = Assert.Single(exportedItems);
        Assert.Same(logRecord, item);

        state.Dispose();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            var state = (IReadOnlyList<KeyValuePair<string, object>>)logRecord.State;

            foreach (var kvp in state)
            {
            }
        });
    }

    [Fact]
    public void CopyMadeWhenLogRecordIsFromThreadStaticPoolTest()
    {
        List<LogRecord> exportedItems = [];

        using var processor = new BatchLogRecordExportProcessor(
#pragma warning disable CA2000 // Dispose objects before losing scope
            new InMemoryExporter<LogRecord>(exportedItems));
#pragma warning restore CA2000 // Dispose objects before losing scope

        var pool = LogRecordThreadStaticPool.Instance;

        var logRecord = pool.Rent();

        processor.OnEnd(logRecord);
        processor.Shutdown();

        var item = Assert.Single(exportedItems);
        Assert.NotSame(logRecord, item);
    }

    [Fact]
    public void LogRecordAddedToBatchIfNotFromAnyPoolTest()
    {
        List<LogRecord> exportedItems = [];

        using var processor = new BatchLogRecordExportProcessor(
#pragma warning disable CA2000 // Dispose objects before losing scope
            new InMemoryExporter<LogRecord>(exportedItems));
#pragma warning restore CA2000 // Dispose objects before losing scope

        var logRecord = new LogRecord();

        processor.OnEnd(logRecord);
        processor.Shutdown();

        var item = Assert.Single(exportedItems);
        Assert.Same(logRecord, item);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LogRecordIsNotReusedWhenSimpleProcessorExporterStopsEnumeratingTest(bool simpleProcessorFirst)
    {
        List<KeyValuePair<string, object?>[]> exportedAttributes = [];

        using var failingExporter = new DelegatingExporter<LogRecord>
        {
            OnExportFunc = batch =>
            {
                foreach (var logRecord in batch)
                {
                    // Leaves the enumerator before it reaches the end.
                    return ExportResult.Failure;
                }

                return ExportResult.Success;
            },
        };

        using var exporter = new DelegatingExporter<LogRecord>
        {
            OnExportFunc = batch =>
            {
                foreach (var logRecord in batch)
                {
                    exportedAttributes.Add([.. logRecord.Attributes!]);
                }

                return ExportResult.Success;
            },
        };

        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddOpenTelemetry(options =>
        {
            if (simpleProcessorFirst)
            {
                options.AddProcessor(new SimpleLogRecordExportProcessor(failingExporter));
            }

            options.AddProcessor(new BatchLogRecordExportProcessor(exporter, scheduledDelayMilliseconds: int.MaxValue));

            if (!simpleProcessorFirst)
            {
                options.AddProcessor(new SimpleLogRecordExportProcessor(failingExporter));
            }
        }));

        using (var serviceProvider = services.BuildServiceProvider())
        {
            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TestLogger");

            for (var i = 0; i < 3; i++)
            {
                var state = new Dictionary<string, object?> { ["index"] = i };

                logger.Log(LogLevel.Information, default, state, exception: null, static (_, _) => "Hello world");
            }
        }

        Assert.Equal(3, exportedAttributes.Count);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal([new("index", i)], exportedAttributes[i]);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisposeWithoutShutdown(bool threadingDisabled)
    {
        using var threadingOverride = ThreadingHelper.BeginThreadingOverride(threadingDisabled);

        var scopeProvider = new LoggerExternalScopeProvider();

        List<LogRecord> exportedItems = [];

        var processor = new BatchLogRecordExportProcessor(
#pragma warning disable CA2000 // Dispose objects before losing scope
            new InMemoryExporter<LogRecord>(exportedItems),
#pragma warning restore CA2000 // Dispose objects before losing scope
            maxQueueSize: BatchLogRecordExportProcessor.DefaultMaxQueueSize,
            scheduledDelayMilliseconds: int.MaxValue,
            exporterTimeoutMilliseconds: BatchLogRecordExportProcessor.DefaultExporterTimeoutMilliseconds,
            maxExportBatchSize: BatchLogRecordExportProcessor.DefaultMaxExportBatchSize);

        processor.Dispose();

        using var scope = scopeProvider.Push(exportedItems);

        var pool = LogRecordSharedPool.Current;

        var logRecord = pool.Rent();

        var state = new LogRecordTests.DisposingState("Hello world");

        logRecord.ILoggerData.ScopeProvider = scopeProvider;
        logRecord.StateValues = state;

        processor.OnEnd(logRecord);

        state.Dispose();

        Assert.Empty(exportedItems);
    }
}
#endif
