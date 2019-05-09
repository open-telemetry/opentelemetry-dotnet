// <copyright file="ExporterOcagentEventSource.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Exporter.Ocagent.Implementation
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Threading;

    [EventSource(Name = "OpenCensus-Exporter-Ocagent")]
    internal class ExporterOcagentEventSource : EventSource
    {
        public static readonly ExporterOcagentEventSource Log = new ExporterOcagentEventSource();

        [NonEvent]
        public void FailedToConvertToProtoDefinitionError(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                this.FailedToConvertToProtoDefinitionError(ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Exporter failed to convert SpanData content into GRPC proto definition. Data will not be sent. Exception: {0}", Level = EventLevel.Error)]
        public void FailedToConvertToProtoDefinitionError(string ex)
        {
            this.WriteEvent(1, ex);
        }

        /// <summary>
        /// Returns a culture-independent string representation of the given <paramref name="exception"/> object,
        /// appropriate for diagnostics tracing.
        /// </summary>
        private static string ToInvariantString(Exception exception)
        {
            CultureInfo originalUICulture = Thread.CurrentThread.CurrentUICulture;

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
