// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Describes a type of <see cref="BaseExporter{Metric}"/> which supports <see cref="ExportModes.Pull"/>.
/// </summary>
public interface IPullMetricExporter
{
    /// <summary>
    /// Gets or sets the Collect delegate.
    /// </summary>
    Func<int, bool>? Collect { get; set; }
}