// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;
#pragma warning disable SA1600 // Elements must be documented

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// ETW EventSource tracing class.
    /// </summary>
    [EventSource(Name = "Microsoft-AspNet-Telemetry-Correlation", Guid = "ace2021e-e82c-5502-d81d-657f27612673")]
    internal sealed class AspNetTelemetryCorrelationEventSource : EventSource
    {
        /// <summary>
        /// Instance of the PlatformEventSource class.
        /// </summary>
        public static readonly AspNetTelemetryCorrelationEventSource Log = new AspNetTelemetryCorrelationEventSource();

        [NonEvent]
        public void ActivityException(string id, string eventName, Exception ex)
        {
            if (IsEnabled(EventLevel.Error, (EventKeywords)(-1)))
            {
                ActivityException(id, eventName, ex.ToString());
            }
        }

        [Event(1, Message = "Callback='{0}'", Level = EventLevel.Verbose)]
        public void TraceCallback(string callback)
        {
            WriteEvent(1, callback);
        }

        [Event(2, Message = "Activity started, Id='{0}'", Level = EventLevel.Verbose)]
        public void ActivityStarted(string id)
        {
            WriteEvent(2, id);
        }

        [Event(3, Message = "Activity stopped, Id='{0}', Name='{1}'", Level = EventLevel.Verbose)]
        public void ActivityStopped(string id, string eventName)
        {
            WriteEvent(3, id, eventName);
        }

        [Event(4, Message = "Failed to parse header '{0}', value: '{1}'", Level = EventLevel.Informational)]
        public void HeaderParsingError(string headerName, string headerValue)
        {
            WriteEvent(4, headerName, headerValue);
        }

        [Event(5, Message = "Failed to extract activity, reason '{0}'", Level = EventLevel.Error)]
        public void ActvityExtractionError(string reason)
        {
            WriteEvent(5, reason);
        }

        [Event(6, Message = "Finished Activity is detected on the stack, Id: '{0}', Name: '{1}'", Level = EventLevel.Error)]
        public void FinishedActivityIsDetected(string id, string name)
        {
            WriteEvent(6, id, name);
        }

        [Event(7, Message = "System.Diagnostics.Activity stack is too deep. This is a code authoring error, Activity will not be stopped.", Level = EventLevel.Error)]
        public void ActivityStackIsTooDeepError()
        {
            WriteEvent(7);
        }

        [Event(8, Message = "Activity restored, Id='{0}'", Level = EventLevel.Informational)]
        public void ActivityRestored(string id)
        {
            WriteEvent(8, id);
        }

        [Event(9, Message = "Failed to invoke OnExecuteRequestStep, Error='{0}'", Level = EventLevel.Error)]
        public void OnExecuteRequestStepInvokationError(string error)
        {
            WriteEvent(9, error);
        }

        [Event(10, Message = "System.Diagnostics.Activity stack is too deep. Current Id: '{0}', Name: '{1}'", Level = EventLevel.Warning)]
        public void ActivityStackIsTooDeepDetails(string id, string name)
        {
            WriteEvent(10, id, name);
        }

        [Event(11, Message = "Activity exception, Id='{0}', Name='{1}': {2}", Level = EventLevel.Error)]
        public void ActivityException(string id, string eventName, string ex)
        {
            WriteEvent(11, id, eventName, ex);
        }
    }
}
#pragma warning restore SA1600 // Elements must be documented