// <copyright file="InstrumentationEventSource.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.Threading;

namespace OpenTelemetry.Instrumentation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Instrumentation")]
    public class InstrumentationEventSource : EventSource
    {
        public static InstrumentationEventSource Log = new InstrumentationEventSource();

        [NonEvent]
        public void ExceptionInCustomSampler(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.ExceptionInCustomSampler(ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Span is NULL or blank in the '{0}' callback. Span will not be recorded.", Level = EventLevel.Warning)]
        public void NullOrBlankSpan(string eventName)
        {
            this.WriteEvent(1, eventName);
        }

        [Event(2, Message = "Error getting custom sampler, the default sampler will be used. Exception : {0}", Level = EventLevel.Warning)]
        public void ExceptionInCustomSampler(string ex)
        {
            this.WriteEvent(2, ex);
        }

        [Event(3, Message = "Current Activity is NULL the '{0}' callback. Span will not be recorded.", Level = EventLevel.Warning)]
        public void NullActivity(string eventName)
        {
            this.WriteEvent(3, eventName);
        }

        [NonEvent]
        public void UnknownErrorProcessingEvent(string handlerName, string eventName, Exception ex)
        {
            if (!this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                return;
            }

            this.UnknownErrorProcessingEvent(handlerName, eventName, ToInvariantString(ex));
        }

        [Event(4, Message = "Unknown error processing event '{0}' from handler '{1}', Exception: {2}", Level = EventLevel.Error)]
        public void UnknownErrorProcessingEvent(string handlerName, string eventName, string ex)
        {
            this.WriteEvent(4, handlerName, eventName, ex);
        }

        [Event(5, Message = "Payload is NULL in '{0}' callback. Span will not be recorded.", Level = EventLevel.Warning)]
        public void NullPayload(string eventName)
        {
            this.WriteEvent(5, eventName);
        }

        [Event(6, Message = "Request is filtered out.", Level = EventLevel.Verbose)]
        public void RequestIsFilteredOut(string eventName)
        {
            this.WriteEvent(6, eventName);
        }

        [NonEvent]
        public void ExceptionInitializingInstrumentation(string instrumentationType, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.ExceptionInitializingInstrumentation(instrumentationType, ToInvariantString(ex));
            }
        }

        [Event(7, Message = "Error initializing instrumentation type {0}. Exception : {1}", Level = EventLevel.Error)]
        public void ExceptionInitializingInstrumentation(string instrumentationType, string ex)
        {
            this.WriteEvent(7, instrumentationType, ex);
        }

        /// <summary>
        /// Returns a culture-independent string representation of the given <paramref name="exception"/> object,
        /// appropriate for diagnostics tracing.
        /// </summary>
        private static string ToInvariantString(Exception exception)
        {
            var originalUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
                return exception.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = originalUICulture;
            }
        }
    }
}
