// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Logs;

/// <summary>
/// A custom processor that routes log records to one of two inner processors
/// based on the log category name. Logs whose category starts with a given
/// prefix are sent to the payment processor; all others go to the default.
/// </summary>
internal sealed class RoutingProcessor : BaseProcessor<LogRecord>
{
    private readonly string categoryPrefix;
    private readonly BaseProcessor<LogRecord> defaultProcessor;
    private readonly BaseProcessor<LogRecord> paymentProcessor;

    public RoutingProcessor(
        string categoryPrefix,
        BaseProcessor<LogRecord> defaultProcessor,
        BaseProcessor<LogRecord> paymentProcessor)
    {
        this.categoryPrefix = categoryPrefix ?? throw new ArgumentNullException(nameof(categoryPrefix));
        this.defaultProcessor = defaultProcessor ?? throw new ArgumentNullException(nameof(defaultProcessor));
        this.paymentProcessor = paymentProcessor ?? throw new ArgumentNullException(nameof(paymentProcessor));
    }

    public override void OnEnd(LogRecord data)
    {
        if (data.CategoryName?.StartsWith(this.categoryPrefix, StringComparison.Ordinal) == true)
        {
            this.paymentProcessor.OnEnd(data);
        }
        else
        {
            this.defaultProcessor.OnEnd(data);
        }
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        var result1 = this.defaultProcessor.ForceFlush(timeoutMilliseconds);
        var result2 = this.paymentProcessor.ForceFlush(timeoutMilliseconds);
        return result1 && result2;
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result1 = this.defaultProcessor.Shutdown(timeoutMilliseconds);
        var result2 = this.paymentProcessor.Shutdown(timeoutMilliseconds);
        return result1 && result2;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.defaultProcessor.Dispose();
            this.paymentProcessor.Dispose();
        }

        base.Dispose(disposing);
    }
}
