// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Options for configuring either a <see cref="BaseExportingMetricReader"/> or <see cref="PeriodicExportingMetricReader"/> .
/// </summary>
public class MetricReaderOptions
{
    private PeriodicExportingMetricReaderOptions periodicExportingMetricReaderOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricReaderOptions"/> class.
    /// </summary>
    public MetricReaderOptions()
        : this(new())
    {
    }

    internal MetricReaderOptions(
        PeriodicExportingMetricReaderOptions defaultPeriodicExportingMetricReaderOptions)
    {
        Debug.Assert(defaultPeriodicExportingMetricReaderOptions != null, "defaultPeriodicExportingMetricReaderOptions was null");

        this.periodicExportingMetricReaderOptions = defaultPeriodicExportingMetricReaderOptions ?? new();
    }

    /// <summary>
    /// Gets or sets the <see cref="MetricReaderTemporalityPreference" />.
    /// </summary>
    public MetricReaderTemporalityPreference TemporalityPreference { get; set; } = MetricReaderTemporalityPreference.Cumulative;

    /// <summary>
    /// Gets or sets the <see cref="Metrics.PeriodicExportingMetricReaderOptions" />.
    /// </summary>
    public PeriodicExportingMetricReaderOptions PeriodicExportingMetricReaderOptions
    {
        get => this.periodicExportingMetricReaderOptions;
        set
        {
            Guard.ThrowIfNull(value);
            this.periodicExportingMetricReaderOptions = value;
        }
    }
}
