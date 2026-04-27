// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Logs;

/// <summary>
/// A custom processor that routes log records to one of two inner processors
/// based on the log category name. Logs whose category starts with a given
/// prefix are sent to a secondary processor; all others go to the primary.
/// </summary>
internal sealed class RoutingProcessor : BaseProcessor<LogRecord>
{
    private readonly string categoryPrefix;
    private readonly BaseProcessor<LogRecord> primaryProcessor;
    private readonly BaseProcessor<LogRecord> secondaryProcessor;

    public RoutingProcessor(
        string categoryPrefix,
        BaseProcessor<LogRecord> primaryProcessor,
        BaseProcessor<LogRecord> secondaryProcessor)
    {
        this.categoryPrefix = categoryPrefix ?? throw new ArgumentNullException(nameof(categoryPrefix));
        this.primaryProcessor = primaryProcessor ?? throw new ArgumentNullException(nameof(primaryProcessor));
        this.secondaryProcessor = secondaryProcessor ?? throw new ArgumentNullException(nameof(secondaryProcessor));
    }

    public override void OnEnd(LogRecord data)
    {
        if (data.CategoryName?.StartsWith(this.categoryPrefix, StringComparison.Ordinal) == true)
        {
            this.secondaryProcessor.OnEnd(data);
        }
        else
        {
            this.primaryProcessor.OnEnd(data);
        }
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        var result1 = this.primaryProcessor.ForceFlush(timeoutMilliseconds);
        var result2 = this.secondaryProcessor.ForceFlush(timeoutMilliseconds);
        return result1 && result2;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result1 = this.primaryProcessor.Shutdown(timeoutMilliseconds);
        var result2 = this.secondaryProcessor.Shutdown(timeoutMilliseconds);
        return result1 && result2;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.primaryProcessor.Dispose();
            this.secondaryProcessor.Dispose();
        }

        base.Dispose(disposing);
    }
}
