// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

namespace OpenTelemetry;

/// <summary>
/// Shared infrastructure for SDK self-observability metrics.
/// </summary>
internal static class SdkSelfObservability
{
    /// <summary>
    /// The name of the <see cref="System.Diagnostics.Metrics.Meter"/> used for SDK self-observability metrics.
    /// </summary>
    /// <remarks>
    /// This is a constant so that it can be referenced without triggering the static
    /// initialization of this class, which would create the instruments.
    /// </remarks>
    internal const string MeterName = "otel.sdk.experimental";

    internal static readonly Meter Meter = MeterFactory.Create(
        typeof(SdkSelfObservability), semanticConventionsVersion: null, name: MeterName);

    internal static readonly Counter<long> LogProcessedCounter = Meter.CreateCounter<long>(
        "otel.sdk.processor.log.processed",
        "{log_record}",
        "The number of log records for which the processing has finished, either successful or failed.");

    internal static readonly Counter<long> SpanProcessedCounter = Meter.CreateCounter<long>(
        "otel.sdk.processor.span.processed",
        "{span}",
        "The number of spans for which the processing has finished, either successful or failed.");
}
