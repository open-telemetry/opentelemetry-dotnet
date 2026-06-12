// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;

namespace OpenTelemetry;

/// <summary>
/// Implements a batch log record export processor.
/// </summary>
public class BatchLogRecordExportProcessor : BatchExportProcessor<LogRecord>
{
    // POC: stable component identity used for the otel.sdk.component.shutdown
    // event. Spec recommends "<type>/<instance-counter>" with a monotonic
    // counter scoped per SDK instance; for the POC we use a process-global
    // counter, which is sufficient for the single-instance test cases.
    private static int instanceCounter = -1;

    private readonly string componentName;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchLogRecordExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter">Log record exporter.</param>
    /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
    /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
    /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
    public BatchLogRecordExportProcessor(
        BaseExporter<LogRecord> exporter,
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
        this.componentName = "batching_log_processor/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        // Note: Intentionally not using Guard.ThrowIfNull to save prod cycles
#pragma warning disable CA1062 // Validate arguments of public methods
        switch (data.Source)
#pragma warning restore CA1062 // Validate arguments of public methods
        {
            case LogRecord.LogRecordSource.FromSharedPool:
                data.Buffer();
                data.AddReference();
                if (!this.TryExport(data))
                {
                    LogRecordSharedPool.Current.Return(data);
                }

                break;

            case LogRecord.LogRecordSource.CreatedManually:
                data.Buffer();
                this.TryExport(data);
                break;

            case LogRecord.LogRecordSource.FromThreadStaticPool:
            default:
                Debug.Assert(data.Source == LogRecord.LogRecordSource.FromThreadStaticPool, "LogRecord source was something unexpected");

                // Note: If we are using ThreadStatic pool we make a copy of the record.
                this.TryExport(data.Copy());
                break;
        }
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        // POC: time the entire shutdown (worker drain + exporter shutdown)
        // so the event reflects wall-clock cost the way the spec defines
        // otel.component.shutdown.duration. Per the spec's Nesting note,
        // this duration mechanically includes the inner exporter's shutdown.
        var startTimestamp = Stopwatch.GetTimestamp();
        var success = base.OnShutdown(timeoutMilliseconds);
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        var result = SdkSelfObservability.ClassifyResult(success, timeoutMilliseconds, elapsed.TotalMilliseconds);
        SdkSelfObservability.EmitComponentShutdown(
            componentType: "batching_log_processor",
            componentName: this.componentName,
            result: result,
            durationSeconds: elapsed.TotalSeconds);
        return success;
    }
}
