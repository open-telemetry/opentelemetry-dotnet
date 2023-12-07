// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Stores configuration for a histogram MetricStream.
/// </summary>
public class HistogramConfiguration : MetricStreamConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether Min, Max
    /// should be collected.
    /// </summary>
    public bool RecordMinMax { get; set; } = true;
}