// <copyright file="RequestsCollectorOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector.AspNetCore
{
    using System;

    /// <summary>
    /// Options for requests collector.
    /// </summary>
    public class RequestsCollectorOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RequestsCollectorOptions"/> class.
        /// </summary>
        public RequestsCollectorOptions()
        {
            this.EventFilter = DefaultFilter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestsCollectorOptions"/> class.
        /// </summary>
        /// <param name="eventFilter">Custom filtering predicate for DiagnosticSource events, if any.</param>
        internal RequestsCollectorOptions(Func<string, object, object, bool> eventFilter = null)
        {
            // TODO This API is unusable and likely to change, let's not expose it for now.

            this.EventFilter = eventFilter;
        }

        /// <summary>
        /// Gets a hook to exclude calls based on domain or other per-request criterion.
        /// </summary>
        internal Func<string, object, object, bool> EventFilter { get; }

        private static bool DefaultFilter(string activityName, object arg1, object unused)
        {
            return true;
        }
    }
}
