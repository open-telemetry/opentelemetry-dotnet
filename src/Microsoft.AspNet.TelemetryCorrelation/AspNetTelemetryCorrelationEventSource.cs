// <copyright file="AspNetTelemetryCorrelationEventSource.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.ActivityException(id, eventName, ex.ToString());
            }
        }

        [Event(1, Message = "Callback='{0}'", Level = EventLevel.Verbose)]
        public void TraceCallback(string callback)
        {
            this.WriteEvent(1, callback);
        }

        [Event(2, Message = "Activity started, Id='{0}'", Level = EventLevel.Verbose)]
        public void ActivityStarted(string id)
        {
            this.WriteEvent(2, id);
        }

        [Event(3, Message = "Activity stopped, Id='{0}', Name='{1}'", Level = EventLevel.Verbose)]
        public void ActivityStopped(string id, string eventName)
        {
            this.WriteEvent(3, id, eventName);
        }

        [Event(4, Message = "Failed to parse header '{0}', value: '{1}'", Level = EventLevel.Informational)]
        public void HeaderParsingError(string headerName, string headerValue)
        {
            this.WriteEvent(4, headerName, headerValue);
        }

        [Event(5, Message = "Failed to extract activity, reason '{0}'", Level = EventLevel.Error)]
        public void ActvityExtractionError(string reason)
        {
            this.WriteEvent(5, reason);
        }

        [Event(6, Message = "Finished Activity is detected on the stack, Id: '{0}', Name: '{1}'", Level = EventLevel.Error)]
        public void FinishedActivityIsDetected(string id, string name)
        {
            this.WriteEvent(6, id, name);
        }

        [Event(7, Message = "System.Diagnostics.Activity stack is too deep. This is a code authoring error, Activity will not be stopped.", Level = EventLevel.Error)]
        public void ActivityStackIsTooDeepError()
        {
            this.WriteEvent(7);
        }

        [Event(8, Message = "Activity restored, Id='{0}'", Level = EventLevel.Informational)]
        public void ActivityRestored(string id)
        {
            this.WriteEvent(8, id);
        }

        [Event(9, Message = "Failed to invoke OnExecuteRequestStep, Error='{0}'", Level = EventLevel.Error)]
        public void OnExecuteRequestStepInvokationError(string error)
        {
            this.WriteEvent(9, error);
        }

        [Event(10, Message = "System.Diagnostics.Activity stack is too deep. Current Id: '{0}', Name: '{1}'", Level = EventLevel.Warning)]
        public void ActivityStackIsTooDeepDetails(string id, string name)
        {
            this.WriteEvent(10, id, name);
        }

        [Event(11, Message = "Activity exception, Id='{0}', Name='{1}': {2}", Level = EventLevel.Error)]
        public void ActivityException(string id, string eventName, string ex)
        {
            this.WriteEvent(11, id, eventName, ex);
        }
    }
}
#pragma warning restore SA1600 // Elements must be documented