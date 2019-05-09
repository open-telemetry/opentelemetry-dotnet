// <copyright file="OpenCensusEventSource.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Implementation
{
    using System;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Threading;

    [EventSource(Name = "OpenCensus-Base")]
    internal class OpenCensusEventSource : EventSource
    {
        public static readonly OpenCensusEventSource Log = new OpenCensusEventSource();

        [NonEvent]
        public void ExporterThrownExceptionWarning(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.ExporterThrownExceptionWarning(ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Exporter failed to export items. Exception: {0}", Level = EventLevel.Warning)]
        public void ExporterThrownExceptionWarning(string ex)
        {
            this.WriteEvent(1, ex);
        }

        [Event(2, Message = "Failed to parse a resource tag. {0} should be an ASCII string with a length greater than 0 and not exceeding {1} characters.", Level = EventLevel.Warning)]
        public void InvalidCharactersInResourceElement(string element)
        {
            this.WriteEvent(2, element, Constants.MaxResourceTypeNameLength);
        }

        [NonEvent]
        public void FailedReadingEnvironmentVariableWarning(string environmentVariableName, Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.FailedReadingEnvironmentVariableWarning(environmentVariableName, ToInvariantString(ex));
            }
        }

        [Event(3, Message = "Failed to read environment variable {0}. Main library failed with security exception: {1}", Level = EventLevel.Warning)]
        public void FailedReadingEnvironmentVariableWarning(string environmentVariableName, string ex)
        {
            this.WriteEvent(3, environmentVariableName, ex);
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
