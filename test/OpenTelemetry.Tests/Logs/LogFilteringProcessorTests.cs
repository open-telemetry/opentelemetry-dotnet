// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable CA2000 // Provider takes ownership of processor/exporter

using OpenTelemetry.Exporter;

namespace OpenTelemetry.Logs.Tests;

public class LogFilteringProcessorTests
{
    [Theory]
    [InlineData(ExportProcessorType.Simple)]
    [InlineData(ExportProcessorType.Batch)]
    public void ExportProcessorCanFilterLogRecords(ExportProcessorType exportProcessorType)
    {
        List<LogRecord> exportedItems = [];

        using var provider = Sdk.CreateLoggerProviderBuilder()
            .AddProcessor(CreateFilteringProcessor(exportProcessorType, exportedItems))
            .Build();

        var logger = provider.GetLogger("TestLogger");

        logger.EmitLog(new LogRecordData { Body = "filtered" });
        logger.EmitLog(new LogRecordData { Body = "exported" });

        Assert.True(provider.ForceFlush());

        var exportedItem = Assert.Single(exportedItems);
        Assert.Equal("exported", exportedItem.Body);
    }

    private static BaseProcessor<LogRecord> CreateFilteringProcessor(
        ExportProcessorType exportProcessorType,
        List<LogRecord> exportedItems)
    {
        var exporter = new InMemoryExporter<LogRecord>(exportedItems);

        return exportProcessorType switch
        {
            ExportProcessorType.Simple => new FilteringSimpleLogRecordExportProcessor(exporter),
            ExportProcessorType.Batch => new FilteringBatchLogRecordExportProcessor(exporter),
            _ => throw new NotSupportedException(),
        };
    }

    private static bool ShouldExport(LogRecord logRecord)
        => logRecord.Body != "filtered";

    private sealed class FilteringSimpleLogRecordExportProcessor : SimpleLogRecordExportProcessor
    {
        public FilteringSimpleLogRecordExportProcessor(BaseExporter<LogRecord> exporter)
            : base(exporter)
        {
        }

        public override void OnEnd(LogRecord data)
        {
            if (ShouldExport(data))
            {
                base.OnEnd(data);
            }
        }
    }

    private sealed class FilteringBatchLogRecordExportProcessor : BatchLogRecordExportProcessor
    {
        public FilteringBatchLogRecordExportProcessor(BaseExporter<LogRecord> exporter)
            : base(exporter)
        {
        }

        public override void OnEnd(LogRecord data)
        {
            if (ShouldExport(data))
            {
                base.OnEnd(data);
            }
        }
    }
}
