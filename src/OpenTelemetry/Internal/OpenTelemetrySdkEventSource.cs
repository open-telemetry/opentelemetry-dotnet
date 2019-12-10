// <copyright file="OpenTelemetrySdkEventSource.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// EventSource implementation for OpenTelemetry SDK implementation.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Sdk")]
    internal class OpenTelemetrySdkEventSource : EventSource
    {
        public static OpenTelemetrySdkEventSource Log = new OpenTelemetrySdkEventSource();

        [NonEvent]
        public void SpanProcessorException(string evnt, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.SpanProcessorException(evnt, ToInvariantString(ex));
            }
        }

        [NonEvent]
        public void SpanContextExtractException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.FailedToExtractSpanContext(ToInvariantString(ex));
            }
        }

        [NonEvent]
        public void TracestateExtractException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.TracestateExtractError(ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Span processor queue size reached maximum. Throttling spans.", Level = EventLevel.Warning)]
        public void SpanProcessorQueueIsExhausted()
        {
            this.WriteEvent(1);
        }

        [Event(2, Message = "Shutdown complete. '{0}' spans left in queue unprocessed.", Level = EventLevel.Informational)]
        public void ShutdownEvent(int spansLeftUnprocessed)
        {
            this.WriteEvent(2, spansLeftUnprocessed);
        }

        [Event(3, Message = "Exporter returned error '{0}'.", Level = EventLevel.Warning)]
        public void ExporterErrorResult(SpanExporter.ExportResult exportResult)
        {
            this.WriteEvent(3, exportResult.ToString());
        }

        [Event(4, Message = "Unknown error in SpanProcessor event '{0}': '{1}'.", Level = EventLevel.Warning)]
        public void SpanProcessorException(string evnt, string ex)
        {
            this.WriteEvent(4, evnt, ex);
        }

        [Event(5, Message = "Calling '{0}' on ended span.", Level = EventLevel.Warning)]
        public void UnexpectedCallOnEndedSpan(string methodName)
        {
            this.WriteEvent(5, methodName);
        }

        [Event(6, Message = "Attempting to dispose scope '{0}' that is not current", Level = EventLevel.Warning)]
        public void AttemptToEndScopeWhichIsNotCurrent(string spanName)
        {
            this.WriteEvent(6, spanName);
        }

        [Event(7, Message = "Attempting to activate active span '{0}'", Level = EventLevel.Warning)]
        public void AttemptToActivateActiveSpan(string spanName)
        {
            this.WriteEvent(7, spanName);
        }

        [Event(8, Message = "Calling method '{0}' with invalid argument '{1}'.", Level = EventLevel.Warning)]
        public void InvalidArgument(string methodName, string issue)
        {
            this.WriteEvent(8, methodName, issue);
        }

        [Event(9, Message = "Failed to extract span context: '{0}'", Level = EventLevel.Warning)]
        public void FailedToExtractSpanContext(string error)
        {
            this.WriteEvent(9, error);
        }

        [Event(10, Message = "Failed to inject span context: '{0}'", Level = EventLevel.Warning)]
        public void FailedToInjectSpanContext(string error)
        {
            this.WriteEvent(10, error);
        }

        [Event(11, Message = "Failed to parse tracestate: too many items", Level = EventLevel.Warning)]
        public void TooManyItemsInTracestate()
        {
            this.WriteEvent(11);
        }

        [Event(12, Message = "Tracestate key is invalid, key = '{0}'", Level = EventLevel.Warning)]
        public void TracestateKeyIsInvalid(ReadOnlySpan<char> key)
        {
            this.WriteEvent(12, key.ToString());
        }

        [Event(13, Message = "Tracestate value is invalid, value = '{0}'", Level = EventLevel.Warning)]
        public void TracestateValueIsInvalid(ReadOnlySpan<char> value)
        {
            this.WriteEvent(13, value.ToString());
        }

        [Event(14, Message = "Tracestate parse error: '{0}'", Level = EventLevel.Warning)]
        public void TracestateExtractError(string error)
        {
            this.WriteEvent(14, error);
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
