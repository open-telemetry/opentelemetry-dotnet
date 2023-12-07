// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Zipkin.Implementation;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Exporter-Zipkin")]
internal sealed class ZipkinExporterEventSource : EventSource
{
    public static ZipkinExporterEventSource Log = new();

    [NonEvent]
    public void FailedExport(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.FailedExport(ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Failed to export activities: '{0}'", Level = EventLevel.Error)]
    public void FailedExport(string exception)
    {
        this.WriteEvent(1, exception);
    }

    [Event(2, Message = "Unsupported attribute type '{0}' for '{1}'. Attribute will not be exported.", Level = EventLevel.Warning)]
    public void UnsupportedAttributeType(string type, string key)
    {
        this.WriteEvent(2, type.ToString(), key);
    }

    [Event(3, Message = "{0} environment variable has an invalid value: '{1}'", Level = EventLevel.Warning)]
    public void InvalidEnvironmentVariable(string key, string value)
    {
        this.WriteEvent(3, key, value);
    }
}