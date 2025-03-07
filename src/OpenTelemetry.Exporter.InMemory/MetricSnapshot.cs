// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// This class represents a selective copy of <see cref="Metric"/>.
/// This contains the minimum fields and properties needed for most
/// unit testing scenarios.
/// </summary>
public class MetricSnapshot
{
    private readonly MetricStreamIdentity instrumentIdentity;

    public MetricSnapshot(Metric metric)
    {
        Guard.ThrowIfNull(metric);
        this.instrumentIdentity = metric.InstrumentIdentity;
        this.MetricType = metric.MetricType;

        List<MetricPoint> metricPoints = [];
        foreach (ref readonly var metricPoint in metric.GetMetricPoints())
        {
            metricPoints.Add(metricPoint.Copy());
        }

        this.MetricPoints = metricPoints;
    }

    public string Name => this.instrumentIdentity.InstrumentName;

    public string Description => this.instrumentIdentity.Description;

    public string Unit => this.instrumentIdentity.Unit;

    public string MeterName => this.instrumentIdentity.MeterName;

    public MetricType MetricType { get; }

    public string MeterVersion => this.instrumentIdentity.MeterVersion;

    public IReadOnlyList<MetricPoint> MetricPoints { get; }
}
