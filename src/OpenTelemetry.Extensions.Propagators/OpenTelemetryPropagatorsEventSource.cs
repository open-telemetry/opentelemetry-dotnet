// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Internal;

/// <summary>
/// EventSource implementation for OpenTelemetry Propagators.
/// This is used for internal logging of this library.
/// </summary>
[EventSource(Name = "OpenTelemetry.Extensions.Propagators")]
internal sealed class OpenTelemetryPropagatorsEventSource : EventSource
{
    public static OpenTelemetryPropagatorsEventSource Log = new();

    [NonEvent]
    public void ActivityContextExtractException(string format, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
        {
            this.FailedToExtractActivityContext(format, ex.ToInvariantString());
        }
    }

    [Event(1, Message = "Failed to extract activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToExtractActivityContext(string format, string exception)
    {
        this.WriteEvent(1, format, exception);
    }

    [Event(2, Message = "Failed to inject activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
    public void FailedToInjectActivityContext(string format, string error)
    {
        this.WriteEvent(2, format, error);
    }
}
