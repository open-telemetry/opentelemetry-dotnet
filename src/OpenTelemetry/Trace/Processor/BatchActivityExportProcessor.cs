// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Implements processor that batches <see cref="Activity"/> objects before calling exporter.
/// </summary>
public class BatchActivityExportProcessor : BatchExportProcessor<Activity>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BatchActivityExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, int, int, int, int)" path="/param[@name='exporter']"/></param>
    /// <param name="maxQueueSize"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, int, int, int, int)" path="/param[@name='maxQueueSize']"/></param>
    /// <param name="scheduledDelayMilliseconds"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, int, int, int, int)" path="/param[@name='scheduledDelayMilliseconds']"/></param>
    /// <param name="exporterTimeoutMilliseconds"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, int, int, int, int)" path="/param[@name='exporterTimeoutMilliseconds']"/></param>
    /// <param name="maxExportBatchSize"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, int, int, int, int)" path="/param[@name='maxExportBatchSize']"/></param>
    public BatchActivityExportProcessor(
        BaseExporter<Activity> exporter,
        int maxQueueSize,
        int scheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds,
        int maxExportBatchSize)
        : this(
            exporter,
            true,
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchActivityExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, bool, int, int, int, int)" path="/param[@name='exporter']"/></param>
    /// <param name="useThreads"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, bool, int, int, int, int)" path="/param[@name='useThread']"/></param>
    /// <param name="maxQueueSize"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, bool, int, int, int, int)" path="/param[@name='maxQueueSize']"/></param>
    /// <param name="scheduledDelayMilliseconds"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, bool, int, int, int, int)" path="/param[@name='scheduledDelayMilliseconds']"/></param>
    /// <param name="exporterTimeoutMilliseconds"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, bool, int, int, int, int)" path="/param[@name='exporterTimeoutMilliseconds']"/></param>
    /// <param name="maxExportBatchSize"><inheritdoc cref="BatchExportProcessor{T}.BatchExportProcessor(BaseExporter{T}, bool, int, int, int, int)" path="/param[@name='maxExportBatchSize']"/></param>
    public BatchActivityExportProcessor(
        BaseExporter<Activity> exporter,
        bool useThreads = true,
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize)
        : base(
            exporter,
            useThreads,
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize)
    {
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        Guard.ThrowIfNull(data);
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        if (!data.Recorded)
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
        {
            return;
        }

        this.OnExport(data);
    }
}
