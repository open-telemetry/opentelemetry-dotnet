// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;

namespace OpenTelemetry;

/// <summary>
/// Implements a simple log record export processor.
/// </summary>
public class SimpleLogRecordExportProcessor : SimpleExportProcessor<LogRecord>
{
    private static int instanceCounter = -1;

    private readonly KeyValuePair<string, object?>[] successTags;
    private readonly KeyValuePair<string, object?>[] alreadyShutdownTags;
    private volatile bool isShutdown;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleLogRecordExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter">Log record exporter.</param>
    public SimpleLogRecordExportProcessor(BaseExporter<LogRecord> exporter)
        : base(exporter)
    {
        var index = Interlocked.Increment(ref instanceCounter);
        var componentName = "simple_log_processor/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var baseTags = new KeyValuePair<string, object?>[]
        {
            new("otel.component.type", "simple_log_processor"),
            new("otel.component.name", componentName),
        };
        this.successTags = baseTags;
        this.alreadyShutdownTags = [.. baseTags, new("error.type", "already_shutdown")];
    }

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        base.OnEnd(data);
        SdkSelfObservability.LogProcessedCounter.Add(
            1, this.isShutdown ? this.alreadyShutdownTags : this.successTags);
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        this.isShutdown = true;
        return base.OnShutdown(timeoutMilliseconds);
    }
}
