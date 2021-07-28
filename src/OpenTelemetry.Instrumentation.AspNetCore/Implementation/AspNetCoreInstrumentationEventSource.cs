// <copyright file="AspNetCoreInstrumentationEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.AspNetCore.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Instrumentation-AspNetCore")]
    internal class AspNetCoreInstrumentationEventSource : EventSource
    {
        public static AspNetCoreInstrumentationEventSource Log = new AspNetCoreInstrumentationEventSource();

        [NonEvent]
        public void RequestFilterException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.RequestFilterException(ex.ToInvariantString());
            }
        }

        [Event(1, Message = "Payload is NULL in event '{1}' from handler '{0}', span will not be recorded.", Level = EventLevel.Warning)]
        public void NullPayload(string handlerName, string eventName)
        {
            this.WriteEvent(1, handlerName, eventName);
        }

        [Event(2, Message = "Request is filtered out.", Level = EventLevel.Verbose)]
        public void RequestIsFilteredOut(string eventName)
        {
            this.WriteEvent(2, eventName);
        }

        [Event(3, Message = "Filter threw exception. Request will not be collected. Exception {0}.", Level = EventLevel.Error)]
        public void RequestFilterException(string exception)
        {
            this.WriteEvent(3, exception);
        }

        [NonEvent]
        public void EnrichmentException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.EnrichmentException(ex.ToInvariantString());
            }
        }

        [Event(4, Message = "Enrich threw exception. Exception {0}.", Level = EventLevel.Error)]
        public void EnrichmentException(string exception)
        {
            this.WriteEvent(4, exception);
        }
    }
}
