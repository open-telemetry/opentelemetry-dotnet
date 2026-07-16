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
    private int activeOnEndCount;
    private int isShutdown;

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
        if (Volatile.Read(ref this.isShutdown) != 0)
        {
            SdkSelfObservability.LogProcessedCounter.Add(1, this.alreadyShutdownTags);
            return;
        }

        Interlocked.Increment(ref this.activeOnEndCount);
        try
        {
            if (Volatile.Read(ref this.isShutdown) != 0)
            {
                SdkSelfObservability.LogProcessedCounter.Add(1, this.alreadyShutdownTags);
                return;
            }

            SdkSelfObservability.LogProcessedCounter.Add(1, this.successTags);
            base.OnEnd(data);
        }
        finally
        {
            Interlocked.Decrement(ref this.activeOnEndCount);
        }
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Interlocked.Exchange(ref this.isShutdown, 1);

        SpinWait spinner = default;
        while (Volatile.Read(ref this.activeOnEndCount) != 0)
        {
            spinner.SpinOnce();
        }

        return base.OnShutdown(timeoutMilliseconds);
    }
}
