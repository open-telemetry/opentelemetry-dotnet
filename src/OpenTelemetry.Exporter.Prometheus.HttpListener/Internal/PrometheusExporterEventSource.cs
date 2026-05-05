// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Exporter-Prometheus")]
internal sealed class PrometheusExporterEventSource : EventSource, IConfigurationExtensionsLogger
{
    public static readonly PrometheusExporterEventSource Log = new();

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

    [NonEvent]
    public void ConflictingType(string metricName, PrometheusType firstType, PrometheusType conflictingType)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.ConflictingType(metricName, firstType.ToString(), conflictingType.ToString());
        }
    }

    [Event(1, Message = "Failed to export metrics: '{0}'", Level = EventLevel.Error)]
    public void FailedExport(string exception)
        => this.WriteEvent(1, exception);

    [Event(2, Message = "Canceled to export metrics: '{0}'", Level = EventLevel.Error)]
    public void CanceledExport(string exception)
        => this.WriteEvent(2, exception);

    [Event(3, Message = "Failed to shutdown Metrics server '{0}'", Level = EventLevel.Error)]
    public void FailedShutdown(string exception)
        => this.WriteEvent(3, exception);

    [Event(4, Message = "No metrics are available for export.", Level = EventLevel.Warning)]
    public void NoMetrics()
        => this.WriteEvent(4);

    [Event(5, Message = "Configuration key '{0}' has an invalid value: '{1}'", Level = EventLevel.Warning)]
    public void InvalidConfigurationValue(string key, string value)
        => this.WriteEvent(5, key, value);

    void IConfigurationExtensionsLogger.LogInvalidConfigurationValue(string key, string value)
        => this.InvalidConfigurationValue(key, value);

    [Event(6, Message = "Dropping metric family '{0}' due to conflicting TYPE metadata '{1}' and '{2}'.", Level = EventLevel.Warning)]
    public void ConflictingType(string metricName, string firstType, string conflictingType)
        => this.WriteEvent(6, metricName, firstType, conflictingType);

    [Event(7, Message = "Dropping duplicate HELP metadata for metric family '{0}' because values '{1}' and '{2}' conflict.", Level = EventLevel.Warning)]
    public void ConflictingHelp(string metricName, string firstHelp, string conflictingHelp)
        => this.WriteEvent(7, metricName, firstHelp, conflictingHelp);

    [Event(8, Message = "Dropping duplicate UNIT metadata for metric family '{0}' because values '{1}' and '{2}' conflict.", Level = EventLevel.Warning)]
    public void ConflictingUnit(string metricName, string firstUnit, string conflictingUnit)
        => this.WriteEvent(8, metricName, firstUnit, conflictingUnit);
}
