// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.GrpcNetClient.Implementation;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Instrumentation-Grpc")]
internal sealed class GrpcInstrumentationEventSource : EventSource
{
    public static GrpcInstrumentationEventSource Log = new();

    [Event(1, Message = "Payload is NULL in event '{1}' from handler '{0}', span will not be recorded.", Level = EventLevel.Warning)]
    public void NullPayload(string handlerName, string eventName)
    {
        this.WriteEvent(1, handlerName, eventName);
    }

    [NonEvent]
    public void EnrichmentException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.EnrichmentException(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void UnknownErrorProcessingEvent(string handlerName, string eventName, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.UnknownErrorProcessingEvent(handlerName, eventName, ex.ToInvariantString());
        }
    }

    [Event(2, Message = "Enrichment threw exception. Exception {0}.", Level = EventLevel.Error)]
    public void EnrichmentException(string exception)
    {
        this.WriteEvent(2, exception);
    }

    [Event(3, Message = "Unknown error processing event '{1}' from handler '{0}', Exception: {2}", Level = EventLevel.Error)]
    public void UnknownErrorProcessingEvent(string handlerName, string eventName, string ex)
    {
        this.WriteEvent(3, handlerName, eventName, ex);
    }
}
