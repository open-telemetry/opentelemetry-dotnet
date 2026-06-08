// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_PrometheusExporterEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<PrometheusExporterEventSource>();
}
