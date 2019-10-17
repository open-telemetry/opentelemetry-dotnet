// <copyright file="AspNetCoreCollectorOptions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Collector.AspNetCore
{
    /// <summary>
    /// Options for requests collector.
    /// </summary>
    public class AspNetCoreCollectorOptions
    {
        /// <summary>
        /// Gets or sets a hook to exclude calls based on domain or other per-request criterion.
        /// </summary>
        internal Func<string, object, object, bool> EventFilter { get; set; } = DefaultFilter;

        private static bool DefaultFilter(string activityName, object arg1, object unused)
        {
            return true;
        }
    }
}
