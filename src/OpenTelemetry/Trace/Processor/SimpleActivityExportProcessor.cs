// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Implements processor that exports <see cref="Activity"/> objects at each OnEnd call.
/// </summary>
public class SimpleActivityExportProcessor : SimpleExportProcessor<Activity>
{
    private static long instanceCounter = -1;

    private readonly KeyValuePair<string, object?>[] successTags;
    private readonly KeyValuePair<string, object?>[] alreadyShutdownTags;

    private int isShutdown;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleActivityExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter"><inheritdoc cref="SimpleExportProcessor{T}.SimpleExportProcessor" path="/param[@name='exporter']"/>.</param>
    public SimpleActivityExportProcessor(BaseExporter<Activity> exporter)
        : base(exporter)
    {
        var index = Interlocked.Increment(ref instanceCounter);
        var componentName = "simple_span_processor/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var baseTags = new KeyValuePair<string, object?>[]
        {
            new("otel.component.type", "simple_span_processor"),
            new("otel.component.name", componentName),
        };
        this.successTags = baseTags;
        this.alreadyShutdownTags = [.. baseTags, new("error.type", "already_shutdown")];
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

        // SimpleActivityExportProcessor exports synchronously per span and is not
        // intended for production use. We accept a benign shutdown race here: a span
        // whose OnEnd runs concurrently with OnShutdown may be exported and counted
        // as processed, or counted as already_shutdown, depending on timing.
        // Exporters are required to be safe against concurrent and post-shutdown
        // Export calls, so this is harmless; we keep the processor simple rather
        // than add barrier synchronization.
        if (Volatile.Read(ref this.isShutdown) != 0)
        {
            SdkSelfObservability.SpanProcessedCounter.Add(1, this.alreadyShutdownTags);
            return;
        }

        SdkSelfObservability.SpanProcessedCounter.Add(1, this.successTags);
        this.OnExport(data);
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        _ = Interlocked.Exchange(ref this.isShutdown, 1);
        return base.OnShutdown(timeoutMilliseconds);
    }
}
