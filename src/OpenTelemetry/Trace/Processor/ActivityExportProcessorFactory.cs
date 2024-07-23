// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal static class ActivityExportProcessorFactory
{
    public static BaseExportProcessor<Activity> CreateBatchExportProcessor(
        BaseExporter<Activity> exporter,
        BatchExportProcessorOptions<Activity> options,
        int pipelineWeight)
    {
        Guard.ThrowIfNull(exporter);
        Guard.ThrowIfNull(options);

        return new BatchActivityExportProcessor(
            exporter,
            options.MaxQueueSize,
            options.ScheduledDelayMilliseconds,
            options.ExporterTimeoutMilliseconds,
            options.MaxExportBatchSize)
        {
            PipelineWeight = pipelineWeight,
        };
    }

    public static BaseExportProcessor<Activity> CreateSimpleExportProcessor(
        BaseExporter<Activity> exporter,
        int pipelineWeight)
    {
        Guard.ThrowIfNull(exporter);

        BaseExportProcessor<Activity> processor = ConcurrencyModesAttribute
            .GetConcurrencyModeForExporter(exporter)
            .HasFlag(ConcurrencyModes.Multithreaded)
                ? new SimpleMultithreadedActivityExportProcessor(exporter)
                : new SimpleActivityExportProcessor(exporter);

        processor.PipelineWeight = pipelineWeight;

        return processor;
    }
}
