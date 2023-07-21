// <copyright file="MetricSnapshot.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
        this.instrumentIdentity = metric.InstrumentIdentity;
        this.MetricType = metric.MetricType;

        List<MetricPoint> metricPoints = new();
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
