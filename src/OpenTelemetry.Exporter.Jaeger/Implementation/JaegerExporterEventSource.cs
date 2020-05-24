﻿// <copyright file="JaegerExporterEventSource.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    /// <summary>
    /// EventSource events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenTelemetry-Exporter-Jaeger")]
    internal class JaegerExporterEventSource : EventSource
    {
        public static JaegerExporterEventSource Log = new JaegerExporterEventSource();

        [NonEvent]
        public void FailedFlush(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.FailedFlush(ToInvariantString(ex));
            }
        }

        [NonEvent]
        public void UnexpectedError(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.UnexpectedError(ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Failed to send spans: '{0}'", Level = EventLevel.Error)]
        public void FailedFlush(string exception)
        {
            this.WriteEvent(1, exception);
        }

        [Event(2, Message = "Error: '{0}'", Level = EventLevel.Error)]
        public void UnexpectedError(string exception)
        {
            this.WriteEvent(1, exception);
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
