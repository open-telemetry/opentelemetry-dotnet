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
    internal static readonly Meter Meter = MeterFactory.Create(
        typeof(SdkSelfObservability), semanticConventionsVersion: null, name: "otel.sdk.experimental");

    internal static readonly Counter<long> LogProcessedCounter = Meter.CreateCounter<long>(
        "otel.sdk.processor.log.processed",
        "{log_record}",
        "The number of log records for which the processing has finished, either successful or failed.");
}
