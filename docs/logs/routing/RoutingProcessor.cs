// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Logs;

/// <summary>
/// A custom processor that routes log records to one of two inner processors
/// based on a user-supplied predicate evaluated at emit time.
/// </summary>
internal sealed class RoutingProcessor : BaseProcessor<LogRecord>
{
    private readonly Func<LogRecord, bool> routeToSecondary;
    private readonly BaseProcessor<LogRecord> primaryProcessor;
    private readonly BaseProcessor<LogRecord> secondaryProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutingProcessor"/> class.
    /// </summary>
    /// <param name="routeToSecondary">
    /// A predicate evaluated for every log record. When it returns
    /// <see langword="true"/> the record is sent to
    /// <paramref name="secondaryProcessor"/>; otherwise it goes to
    /// <paramref name="primaryProcessor"/>.
    /// </param>
    /// <param name="primaryProcessor">The default export processor.</param>
    /// <param name="secondaryProcessor">The alternative export processor.</param>
    public RoutingProcessor(
        Func<LogRecord, bool> routeToSecondary,
        BaseProcessor<LogRecord> primaryProcessor,
        BaseProcessor<LogRecord> secondaryProcessor)
    {
        this.routeToSecondary = routeToSecondary ?? throw new ArgumentNullException(nameof(routeToSecondary));
        this.primaryProcessor = primaryProcessor ?? throw new ArgumentNullException(nameof(primaryProcessor));
        this.secondaryProcessor = secondaryProcessor ?? throw new ArgumentNullException(nameof(secondaryProcessor));
    }

    public override void OnEnd(LogRecord data)
    {
        if (this.routeToSecondary(data))
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
        return this.primaryProcessor.ForceFlush(timeoutMilliseconds)
            && this.secondaryProcessor.ForceFlush(timeoutMilliseconds);
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.primaryProcessor.Shutdown(timeoutMilliseconds)
            && this.secondaryProcessor.Shutdown(timeoutMilliseconds);
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
