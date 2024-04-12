// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.Http.Implementation;

/// <summary>
/// EventSource events emitted from the project.
/// </summary>
[EventSource(Name = "OpenTelemetry-Instrumentation-Http")]
internal sealed class HttpInstrumentationEventSource : EventSource, IConfigurationExtensionsLogger
{
    public static HttpInstrumentationEventSource Log = new();

    [NonEvent]
    public void FailedProcessResult(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.FailedProcessResult(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void RequestFilterException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.RequestFilterException(ex.ToInvariantString());
        }
    }

    [NonEvent]
    public void ExceptionInitializingInstrumentation(string instrumentationType, Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.ExceptionInitializingInstrumentation(instrumentationType, ex.ToInvariantString());
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

    [Event(1, Message = "Failed to process result: '{0}'", Level = EventLevel.Error)]
    public void FailedProcessResult(string ex)
    {
        this.WriteEvent(1, ex);
    }

    [Event(2, Message = "Error initializing instrumentation type {0}. Exception : {1}", Level = EventLevel.Error)]
    public void ExceptionInitializingInstrumentation(string instrumentationType, string ex)
    {
        this.WriteEvent(2, instrumentationType, ex);
    }

    [Event(3, Message = "Payload is NULL in event '{1}' from handler '{0}', span will not be recorded.", Level = EventLevel.Warning)]
    public void NullPayload(string handlerName, string eventName)
    {
        this.WriteEvent(3, handlerName, eventName);
    }

    [Event(4, Message = "Filter threw exception. Request will not be collected. Exception {0}.", Level = EventLevel.Error)]
    public void RequestFilterException(string exception)
    {
        this.WriteEvent(4, exception);
    }

    [NonEvent]
    public void EnrichmentException(Exception ex)
    {
        if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            this.EnrichmentException(ex.ToInvariantString());
        }
    }

    [Event(5, Message = "Enrich threw exception. Exception {0}.", Level = EventLevel.Error)]
    public void EnrichmentException(string exception)
    {
        this.WriteEvent(5, exception);
    }

    [Event(6, Message = "Request is filtered out.", Level = EventLevel.Verbose)]
    public void RequestIsFilteredOut(string eventName)
    {
        this.WriteEvent(6, eventName);
    }

    [Event(7, Message = "Unknown error processing event '{1}' from handler '{0}', Exception: {2}", Level = EventLevel.Error)]
    public void UnknownErrorProcessingEvent(string handlerName, string eventName, string ex)
    {
        this.WriteEvent(7, handlerName, eventName, ex);
    }

    [Event(8, Message = "Configuration key '{0}' has an invalid value: '{1}'", Level = EventLevel.Warning)]
    public void InvalidConfigurationValue(string key, string value)
    {
        this.WriteEvent(8, key, value);
    }

    void IConfigurationExtensionsLogger.LogInvalidConfigurationValue(string key, string value)
    {
        this.InvalidConfigurationValue(key, value);
    }
}
