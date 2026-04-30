// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// Contains batch export processor options.
/// </summary>
/// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
public class BatchExportProcessorOptions<T>
    where T : class
{
    /// <summary>
    /// Gets or sets the maximum queue size. The queue drops the data if the maximum size is reached. The default value is 2048.
    /// </summary>
    public int MaxQueueSize { get; set; } = BatchExportProcessor<T>.DefaultMaxQueueSize;

    /// <summary>
    /// Gets or sets the delay interval (in milliseconds) between two consecutive exports. The default value is 5000.
    /// </summary>
    public int ScheduledDelayMilliseconds { get; set; } = BatchExportProcessor<T>.DefaultScheduledDelayMilliseconds;

    /// <summary>
    /// Gets or sets the timeout (in milliseconds) after which the export is cancelled. The default value is 30000.
    /// </summary>
    public int ExporterTimeoutMilliseconds { get; set; } = BatchExportProcessor<T>.DefaultExporterTimeoutMilliseconds;

    /// <summary>
    /// Gets or sets the maximum batch size of every export. It must be smaller or equal to <see cref="MaxQueueSize"/>. The default value is 512.
    /// </summary>
    public int MaxExportBatchSize { get; set; } = BatchExportProcessor<T>.DefaultMaxExportBatchSize;

    internal static void ApplyValidatedConfiguration(
        BatchExportProcessorOptions<T> options,
        int maxQueueSize,
        int maxExportBatchSize,
        int exporterTimeoutMilliseconds,
        int scheduledDelayMilliseconds)
    {
        var candidateMaxQueueSize = options.MaxQueueSize;
        var candidateMaxExportBatchSize = options.MaxExportBatchSize;
        var candidateExporterTimeoutMilliseconds = options.ExporterTimeoutMilliseconds;
        var candidateScheduledDelayMilliseconds = options.ScheduledDelayMilliseconds;

        if (maxQueueSize > 0)
        {
            candidateMaxQueueSize = maxQueueSize;
        }

        if (maxExportBatchSize > 0)
        {
            candidateMaxExportBatchSize = maxExportBatchSize;
        }

        if (candidateMaxExportBatchSize > candidateMaxQueueSize)
        {
            candidateMaxExportBatchSize = candidateMaxQueueSize;
        }

        if (exporterTimeoutMilliseconds >= 0)
        {
            candidateExporterTimeoutMilliseconds = exporterTimeoutMilliseconds;
        }

        if (scheduledDelayMilliseconds > 0)
        {
            candidateScheduledDelayMilliseconds = scheduledDelayMilliseconds;
        }

        options.MaxQueueSize = candidateMaxQueueSize;
        options.MaxExportBatchSize = candidateMaxExportBatchSize;
        options.ExporterTimeoutMilliseconds = candidateExporterTimeoutMilliseconds;
        options.ScheduledDelayMilliseconds = candidateScheduledDelayMilliseconds;
    }
}
