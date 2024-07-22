// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal static class ActivityExportProcessorFactory
{
    public static BaseExportProcessor<Activity> CreateBatchExportProcessor(
        ActivityExportProcessorOptions options,
        BaseExporter<Activity> exporter)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(exporter);

        return new BatchActivityExportProcessor(
            exporter,
            options.BatchExportProcessorOptions.MaxQueueSize,
            options.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
            options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
            options.BatchExportProcessorOptions.MaxExportBatchSize)
        {
            PipelineWeight = options.PipelineWeight,
        };
    }

    public static BaseExportProcessor<Activity> CreateSimpleExportProcessor(
        ActivityExportProcessorOptions options,
        BaseExporter<Activity> exporter)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(exporter);

        var concurrencyMode = exporter
            .GetType()
            .GetCustomAttribute<ConcurrencyModesAttribute>(inherit: true)?.Supported
            ?? ConcurrencyModes.Reentrant;

        if (!concurrencyMode.HasFlag(ConcurrencyModes.Reentrant))
        {
            throw new NotSupportedException("Non-reentrant simple export processors are not currently supported.");
        }

        BaseExportProcessor<Activity> processor = concurrencyMode.HasFlag(ConcurrencyModes.Multithreaded)
            ? new SimpleMultithreadedActivityExportProcessor(exporter)
            : new SimpleActivityExportProcessor(exporter);

        processor.PipelineWeight = options.PipelineWeight;

        return processor;
    }
}
