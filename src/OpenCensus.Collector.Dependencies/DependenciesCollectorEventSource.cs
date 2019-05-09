// <copyright file="DependenciesCollectorEventSource.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Collector.Dependencies
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Globalization;
    using System.Text;
    using System.Threading;

    /// <summary>
    /// EventSource listing ETW events emitted from the project.
    /// </summary>
    [EventSource(Name = "OpenCensus.Collector.Dependencies")]
    internal class DependenciesCollectorEventSource : EventSource
    {
        internal static DependenciesCollectorEventSource Log = new DependenciesCollectorEventSource();

        [NonEvent]
        public void ExceptionInCustomSampler(Exception ex)
        {
            if (Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.ExceptionInCustomSampler(ToInvariantString(ex));
            }
        }

        [Event(1, Message = "Context is NULL in end callback. Span will not be recorded.", Level = EventLevel.Warning)]
        public void NullContext()
        {
            this.WriteEvent(1);
        }

        [Event(2, Message = "Error getting custom sampler, the default sampler will be used. Exception : {0}", Level = EventLevel.Warning)]
        public void ExceptionInCustomSampler(string ex)
        {
            this.WriteEvent(2, ex);
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
