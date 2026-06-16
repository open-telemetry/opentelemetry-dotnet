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
    private readonly BatchExportProcessorOptions<T>? defaults;
    private int? maxQueueSize;
    private int? scheduledDelayMilliseconds;
    private int? exporterTimeoutMilliseconds;
    private int? maxExportBatchSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchExportProcessorOptions{T}"/> class.
    /// </summary>
    public BatchExportProcessorOptions()
    {
    }

    internal BatchExportProcessorOptions(BatchExportProcessorOptions<T> defaults)
    {
        this.defaults = defaults;
    }

    /// <summary>
    /// Gets or sets the maximum queue size. The queue drops the data if the maximum size is reached. The default value is 2048.
    /// </summary>
    public int MaxQueueSize
    {
        get => this.maxQueueSize ?? this.defaults?.MaxQueueSize ?? BatchExportProcessor<T>.DefaultMaxQueueSize;
        set => this.maxQueueSize = value;
    }

    /// <summary>
    /// Gets or sets the delay interval (in milliseconds) between two consecutive exports. The default value is 5000.
    /// </summary>
    public int ScheduledDelayMilliseconds
    {
        get => this.scheduledDelayMilliseconds ?? this.defaults?.ScheduledDelayMilliseconds ?? BatchExportProcessor<T>.DefaultScheduledDelayMilliseconds;
        set => this.scheduledDelayMilliseconds = value;
    }

    /// <summary>
    /// Gets or sets the timeout (in milliseconds) after which the export is cancelled. The default value is 30000.
    /// </summary>
    public int ExporterTimeoutMilliseconds
    {
        get => this.exporterTimeoutMilliseconds ?? this.defaults?.ExporterTimeoutMilliseconds ?? BatchExportProcessor<T>.DefaultExporterTimeoutMilliseconds;
        set => this.exporterTimeoutMilliseconds = value;
    }

    /// <summary>
    /// Gets or sets the maximum batch size of every export. It must be smaller or equal to <see cref="MaxQueueSize"/>. The default value is 512.
    /// </summary>
    public int MaxExportBatchSize
    {
        get => this.maxExportBatchSize ?? this.defaults?.MaxExportBatchSize ?? BatchExportProcessor<T>.DefaultMaxExportBatchSize;
        set => this.maxExportBatchSize = value;
    }

    internal int? MaxQueueSizeValue => this.maxQueueSize;

    internal int? ScheduledDelayMillisecondsValue => this.scheduledDelayMilliseconds;

    internal int? ExporterTimeoutMillisecondsValue => this.exporterTimeoutMilliseconds;

    internal int? MaxExportBatchSizeValue => this.maxExportBatchSize;

    internal static void ApplyValidatedConfiguration(
        BatchExportProcessorOptions<T> options,
        int? maxQueueSize,
        int? maxExportBatchSize,
        int? exporterTimeoutMilliseconds,
        int? scheduledDelayMilliseconds)
    {
        int? candidateMaxQueueSize = null;
        int? candidateMaxExportBatchSize = null;
        int? candidateExporterTimeoutMilliseconds = null;
        int? candidateScheduledDelayMilliseconds = null;

        if (maxQueueSize > 0)
        {
            candidateMaxQueueSize = maxQueueSize;
        }

        if (maxExportBatchSize > 0)
        {
            candidateMaxExportBatchSize = maxExportBatchSize;
        }

        var effectiveMaxQueueSize = candidateMaxQueueSize ?? options.MaxQueueSize;
        var effectiveMaxExportBatchSize = candidateMaxExportBatchSize ?? options.MaxExportBatchSize;

        if (effectiveMaxExportBatchSize > effectiveMaxQueueSize)
        {
            candidateMaxExportBatchSize = effectiveMaxQueueSize;
        }

        if (exporterTimeoutMilliseconds >= 0)
        {
            candidateExporterTimeoutMilliseconds = exporterTimeoutMilliseconds;
        }

        if (scheduledDelayMilliseconds > 0)
        {
            candidateScheduledDelayMilliseconds = scheduledDelayMilliseconds;
        }

        options.maxQueueSize = candidateMaxQueueSize;
        options.maxExportBatchSize = candidateMaxExportBatchSize;
        options.exporterTimeoutMilliseconds = candidateExporterTimeoutMilliseconds;
        options.scheduledDelayMilliseconds = candidateScheduledDelayMilliseconds;
    }
}
