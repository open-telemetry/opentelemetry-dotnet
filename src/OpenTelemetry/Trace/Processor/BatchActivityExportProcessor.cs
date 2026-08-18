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
    private static long instanceCounter = -1;

    private readonly KeyValuePair<string, object?>[] successTags;
    private readonly KeyValuePair<string, object?>[] queueFullTags;
    private readonly KeyValuePair<string, object?>[] alreadyShutdownTags;

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
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize)
        : base(
            exporter,
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize)
    {
        var index = Interlocked.Increment(ref instanceCounter);
        var componentName = "batching_span_processor/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var baseTags = new KeyValuePair<string, object?>[]
        {
            new("otel.component.type", "batching_span_processor"),
            new("otel.component.name", componentName),
        };
        this.successTags = baseTags;
        this.queueFullTags = [.. baseTags, new("error.type", "queue_full")];
        this.alreadyShutdownTags = [.. baseTags, new("error.type", "already_shutdown")];
        this.ExportStarted = this.RecordSuccessfulProcessing;
        this.RegisterQueueMetrics(isLogProcessor: false, baseTags);
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        Guard.ThrowIfNull(data);
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        if (!data.Recorded)
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
        {
            if (data.IsAllDataRequested)
            {
                // RECORD_ONLY: the span reaches the processor but by design is never
                // handed to an exporter, so its processing is complete and successful.
                SdkSelfObservability.SpanProcessedCounter.Add(1, this.successTags);
            }

            // Note: TracerProviderSdk does not invoke processors for activities with
            // IsAllDataRequested set to false (spans the sampler dropped), so that case is
            // only reachable when OnEnd is called directly. Such spans are not counted.
            return;
        }

        if (!this.TryEnterOnEnd())
        {
            SdkSelfObservability.SpanProcessedCounter.Add(1, this.alreadyShutdownTags);
            return;
        }

        try
        {
            this.OnExport(data);
        }
        finally
        {
            this.ExitOnEnd();
        }
    }

    // TODO: https://github.com/open-telemetry/opentelemetry-dotnet/issues/7586
    // Consider an ObservableCounter instead of per-item Counter.Add().
    internal override void OnItemDropped()
        => SdkSelfObservability.SpanProcessedCounter.Add(1, this.queueFullTags);

    private void RecordSuccessfulProcessing(long count)
        => SdkSelfObservability.SpanProcessedCounter.Add(count, this.successTags);
}
