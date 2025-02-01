// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Exporter-Prometheus")]
internal sealed class PrometheusExporterEventSource : EventSource
{
    public static PrometheusExporterEventSource Log = new();

    [NonEvent]
    public void FailedExport(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.FailedExport(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void FailedShutdown(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.FailedShutdown(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void CanceledExport(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.CanceledExport(ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Failed to export metrics: '{0}'", Level = EventLevel.Error)]
    public void FailedExport(string exception)
    {
        this.WriteEvent(1, exception);
    }

    [Event(2, Message = "Canceled to export metrics: '{0}'", Level = EventLevel.Error)]
    public void CanceledExport(string exception)
    {
        this.WriteEvent(2, exception);
    }

    [Event(3, Message = "Failed to shutdown Metrics server '{0}'", Level = EventLevel.Error)]
    public void FailedShutdown(string exception)
    {
        this.WriteEvent(3, exception);
    }

    [Event(4, Message = "No metrics are available for export.", Level = EventLevel.Warning)]
    public void NoMetrics()
    {
        this.WriteEvent(4);
    }

    [Event(5, Message = "Ignoring exemplar tags that are too long for metric: '{0}'", Level = EventLevel.Warning)]
    public void ExemplarTagsTooLong(string metricName)
    {
        this.WriteEvent(5, metricName);
    }
}
