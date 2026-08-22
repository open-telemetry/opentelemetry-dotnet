// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;

namespace OpenTelemetry.Logs.Tests;

public sealed class LogRecordExportProcessorTests
{
    [Theory]
    [InlineData(ExportProcessorType.Simple)]
    [InlineData(ExportProcessorType.Batch)]
    public void ExportProcessorIgnoresDroppedLogRecord(ExportProcessorType exportProcessorType)
    {
        List<LogRecord> exportedItems = [];

        using var provider = Sdk.CreateLoggerProviderBuilder()
#pragma warning disable CA2000 // Dispose objects before losing scope
            .AddProcessor(new DropLogRecordProcessor())
            .AddProcessor(CreateExportProcessor(exportProcessorType, exportedItems))
#pragma warning restore CA2000 // Dispose objects before losing scope
            .Build();

        var logger = provider.GetLogger("TestLogger");

        logger.EmitLog(new() { Body = DropLogRecordProcessor.DroppedBody });
        logger.EmitLog(new() { Body = "Hello world" });

        provider.ForceFlush();

        var exportedItem = Assert.Single(exportedItems);
        Assert.Equal("Hello world", exportedItem.Body);
        Assert.False(exportedItem.Dropped);
    }

    private static BaseProcessor<LogRecord> CreateExportProcessor(
        ExportProcessorType exportProcessorType,
        List<LogRecord> exportedItems)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new InMemoryExporter<LogRecord>(exportedItems);
#pragma warning restore CA2000 // Dispose objects before losing scope

        return exportProcessorType switch
        {
            ExportProcessorType.Simple => new SimpleLogRecordExportProcessor(exporter),
            ExportProcessorType.Batch => new BatchLogRecordExportProcessor(exporter),
            _ => throw new NotSupportedException(),
        };
    }

    private sealed class DropLogRecordProcessor : BaseProcessor<LogRecord>
    {
        internal const string DroppedBody = "drop";

        public override void OnEnd(LogRecord data)
        {
            if (data.Body == DroppedBody)
            {
                data.Dropped = true;
            }
        }
    }
}
