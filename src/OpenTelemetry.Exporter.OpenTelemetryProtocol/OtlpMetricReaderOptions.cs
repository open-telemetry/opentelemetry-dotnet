// <copyright file="OtlpMetricReaderOptions.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Options for configuring the <see cref="MetricReader"/> used in conjunction with an <see cref="OtlpMetricExporter"/>.
/// This is meant to be used with the MeterProviderBuilder.AddOtlpExporter() extension method.
/// </summary>
public class OtlpMetricReaderOptions
{
    /// <summary>
    /// Gets or sets the AggregationTemporality used for Histogram
    /// and Sum metrics.
    /// </summary>
    public AggregationTemporality Temporality { get; set; } = AggregationTemporality.Cumulative;

    /// <summary>
    /// Gets or sets the <see cref="MetricReaderType" /> to use. Defaults to <c>MetricReaderType.Manual</c>.
    /// </summary>
    public MetricReaderType MetricReaderType { get; set; } = MetricReaderType.Manual;

    /// <summary>
    /// Gets or sets the <see cref="PeriodicExportingMetricReaderOptions" /> options. Ignored unless <c>MetricReaderType</c> is <c>Periodic</c>.
    /// </summary>
    public PeriodicExportingMetricReaderOptions PeriodicExportingMetricReaderOptions { get; set; } = new PeriodicExportingMetricReaderOptions();
}
