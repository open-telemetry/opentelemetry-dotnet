// <copyright file="OpenTelemetryApiEventSource.cs" company="OpenTelemetry Authors">
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
