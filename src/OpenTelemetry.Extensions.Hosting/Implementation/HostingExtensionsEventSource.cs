// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Extensions.Hosting.Implementation;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Extensions-Hosting")]
internal sealed class HostingExtensionsEventSource : EventSource
{
    public static readonly HostingExtensionsEventSource Log = new();

    [Event(1, Message = "OpenTelemetry TracerProvider was not found in application services. Tracing will remain disabled.", Level = EventLevel.Warning)]
    public void TracerProviderNotRegistered()
    {
        this.WriteEvent(1);
    }

    [Event(2, Message = "OpenTelemetry MeterProvider was not found in application services. Metrics will remain disabled.", Level = EventLevel.Warning)]
    public void MeterProviderNotRegistered()
    {
        this.WriteEvent(2);
    }

    [Event(3, Message = "OpenTelemetry LoggerProvider was not found in application services. Logging will remain disabled.", Level = EventLevel.Warning)]
    public void LoggerProviderNotRegistered()
    {
        this.WriteEvent(3);
    }
}
