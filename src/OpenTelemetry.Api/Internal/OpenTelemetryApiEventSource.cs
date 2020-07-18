// <copyright file="OpenTelemetryApiEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// EventSource implementation for OpenTelemetry API.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Api")]
    internal class OpenTelemetryApiEventSource : EventSource
    {
        public static OpenTelemetryApiEventSource Log = new OpenTelemetryApiEventSource();

        [NonEvent]
        public void SpanContextExtractException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.FailedToExtractSpanContext(ToInvariantString(ex));
            }
        }

        [NonEvent]
        public void ActivityContextExtractException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.FailedToExtractActivityContext(ToInvariantString(ex));
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

        [NonEvent]
        public void TracestateKeyIsInvalid(ReadOnlySpan<char> key)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.TracestateKeyIsInvalid(key.ToString());
            }
        }

        [NonEvent]
        public void TracestateValueIsInvalid(ReadOnlySpan<char> value)
        {
            if (this.IsEnabled(EventLevel.Warning, (EventKeywords)(-1)))
            {
                this.TracestateValueIsInvalid(value.ToString());
            }
        }

        [Event(1, Message = "Failed to extract span context: '{0}'", Level = EventLevel.Warning)]
        public void FailedToExtractSpanContext(string exception)
        {
            this.WriteEvent(1, exception);
        }

        [Event(2, Message = "Failed to inject span context: '{0}'", Level = EventLevel.Warning)]
        public void FailedToInjectSpanContext(string error)
        {
            this.WriteEvent(2, error);
        }

        [Event(3, Message = "Failed to parse tracestate: too many items", Level = EventLevel.Warning)]
        public void TooManyItemsInTracestate()
        {
            this.WriteEvent(3);
        }

        [Event(4, Message = "Tracestate key is invalid, key = '{0}'", Level = EventLevel.Warning)]
        public void TracestateKeyIsInvalid(string key)
        {
            this.WriteEvent(4, key);
        }

        [Event(5, Message = "Tracestate value is invalid, value = '{0}'", Level = EventLevel.Warning)]
        public void TracestateValueIsInvalid(string value)
        {
            this.WriteEvent(5, value);
        }

        [Event(6, Message = "Tracestate parse error: '{0}'", Level = EventLevel.Warning)]
        public void TracestateExtractError(string error)
        {
            this.WriteEvent(6, error);
        }

        [Event(7, Message = "Calling method '{0}' with invalid argument '{1}', issue '{2}'.", Level = EventLevel.Warning)]
        public void InvalidArgument(string methodName, string argumentName, string issue)
        {
            this.WriteEvent(7, methodName, argumentName, issue);
        }

        [Event(8, Message = "Failed to extract activity context: '{0}'", Level = EventLevel.Warning)]
        public void FailedToExtractActivityContext(string exception)
        {
            this.WriteEvent(8, exception);
        }

        [Event(9, Message = "Failed to inject activity context: '{0}'", Level = EventLevel.Warning)]
        public void FailedToInjectActivityContext(string error)
        {
            this.WriteEvent(9, error);
        }

        [Event(10, Message = "Failed to extract span context: '{0}'", Level = EventLevel.Warning)]
        public void FailedToExtractContext(string error)
        {
            this.WriteEvent(10, error);
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
