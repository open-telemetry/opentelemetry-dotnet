// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

#pragma warning disable CA1515 // Consider making public types internal
public class MetricsMappingTestData
#pragma warning restore CA1515 // Consider making public types internal
{
    internal MetricsMappingTestData(MetricType openTelemetryMetricType, PrometheusType expectedPrometheusType)
    {
        this.OpenTelemetryMetricType = openTelemetryMetricType;
        this.ExpectedPrometheusType = expectedPrometheusType;
    }

    internal MetricType OpenTelemetryMetricType { get; }

    internal PrometheusType ExpectedPrometheusType { get; }
}
