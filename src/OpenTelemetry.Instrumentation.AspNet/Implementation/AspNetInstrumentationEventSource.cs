// <copyright file="AspNetInstrumentationEventSource.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Instrumentation.AspNet.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Instrumentation-AspNet")]
    internal sealed class AspNetInstrumentationEventSource : EventSource
    {
        public static AspNetInstrumentationEventSource Log = new AspNetInstrumentationEventSource();

        [NonEvent]
        public void RequestFilterException(string operationName, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.RequestFilterException(operationName, ex.ToInvariantString());
            }
        }

        [NonEvent]
        public void EnrichmentException(string eventName, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.EnrichmentException(eventName, ex.ToInvariantString());
            }
        }

        [Event(1, Message = "Request is filtered out and will not be collected. Operation='{0}'", Level = EventLevel.Verbose)]
        public void RequestIsFilteredOut(string operationName)
        {
            this.WriteEvent(1, operationName);
        }

        [Event(2, Message = "Filter callback threw an exception. Request will not be collected. Operation='{0}': {1}", Level = EventLevel.Error)]
        public void RequestFilterException(string operationName, string exception)
        {
            this.WriteEvent(2, operationName, exception);
        }

        [Event(3, Message = "Enrich callback threw an exception. Event='{0}': {1}", Level = EventLevel.Error)]
        public void EnrichmentException(string eventName, string exception)
        {
            this.WriteEvent(3, eventName, exception);
        }
    }
}
