// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace OpenTelemetry;

/// <summary>
/// Shared infrastructure for SDK self-observability metrics.
/// </summary>
internal static class SdkSelfObservability
{
#pragma warning disable SA1202 // internal before private - initialization order requires Meter first
    private static readonly Meter Meter = new(new MeterOptions("otel.sdk.experimental")
    {
        Version = typeof(SdkSelfObservability).Assembly.GetName().Version?.ToString(),
    });

    internal static readonly Counter<long> LogProcessedCounter = Meter.CreateCounter<long>(
        "otel.sdk.processor.log.processed",
        "{log_record}",
        "The number of log records for which the processing has finished, either successful or failed.");
#pragma warning restore SA1202
}
