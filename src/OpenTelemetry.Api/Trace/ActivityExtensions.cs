// <copyright file="ActivityExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods on Activity.
    /// </summary>
    public static class ActivityExtensions
    {
        private const string StatusCodeKey = "ot.status_code";
        private const string StatusDescriptionKey = "ot.status_description";

        /// <summary>
        /// Sets the status of activity execution.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <param name="status">Activity execution status.</param>
        public static void SetStatus(this Activity activity, Status status)
        {
            activity?.AddTag(StatusCodeKey, status.CanonicalCode.ToString());
            activity?.AddTag(StatusDescriptionKey, status.Description);
        }

        /// <summary>
        /// Gets the status of activity execution.
        /// </summary>
        /// <param name="activity">Activity instance.</param>
        /// <returns>Activity execution status.</returns>
        public static Status GetStatus(this Activity activity)
        {
            var statusCode = activity?.Tags.FirstOrDefault(k => k.Key == StatusCodeKey).Value;
            var statusDescription = activity?.Tags.FirstOrDefault(d => d.Key == StatusDescriptionKey).Value;

            Enum.TryParse(statusCode, out StatusCanonicalCode statusCanonicalCode);

            return new Status(statusCanonicalCode, statusDescription);
        }
    }
}
