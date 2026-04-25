// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Logs;

/// <summary>
/// A custom processor that routes log records to different export processors
/// based on the value of a Baggage entry.
/// </summary>
internal sealed class BaggageRoutingProcessor : BaseProcessor<LogRecord>
{
    private readonly string baggageKey;
    private readonly string baggageValueForSecondary;
    private readonly BaseProcessor<LogRecord> primaryProcessor;
    private readonly BaseProcessor<LogRecord> secondaryProcessor;

    public BaggageRoutingProcessor(
        string baggageKey,
        string baggageValueForSecondary,
        BaseProcessor<LogRecord> primaryProcessor,
        BaseProcessor<LogRecord> secondaryProcessor)
    {
        this.baggageKey = baggageKey ?? throw new ArgumentNullException(nameof(baggageKey));
        this.baggageValueForSecondary = baggageValueForSecondary ?? throw new ArgumentNullException(nameof(baggageValueForSecondary));
        this.primaryProcessor = primaryProcessor ?? throw new ArgumentNullException(nameof(primaryProcessor));
        this.secondaryProcessor = secondaryProcessor ?? throw new ArgumentNullException(nameof(secondaryProcessor));
    }

    public override void OnEnd(LogRecord data)
    {
        var baggageValue = Baggage.GetBaggage(this.baggageKey);

        if (string.Equals(baggageValue, this.baggageValueForSecondary, StringComparison.Ordinal))
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
