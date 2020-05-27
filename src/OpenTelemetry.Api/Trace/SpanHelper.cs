// <copyright file="SpanHelper.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A collection of helper methods to be used when building spans.
    /// </summary>
    public static class SpanHelper
    {
        private static readonly ConcurrentDictionary<StatusCanonicalCode, string> CanonicalCodeToStringCache = new ConcurrentDictionary<StatusCanonicalCode, string>();

        /// <summary>
        /// Helper method that returns the string version of a <see cref="StatusCanonicalCode"/> using a cache to save on allocations.
        /// </summary>
        /// <param name="statusCanonicalCode"><see cref="StatusCanonicalCode"/>.</param>
        /// <returns>String version of the supplied <see cref="StatusCanonicalCode"/>.</returns>
        public static string GetCachedCanonicalCodeString(StatusCanonicalCode statusCanonicalCode)
        {
            if (!CanonicalCodeToStringCache.TryGetValue(statusCanonicalCode, out string canonicalCode))
            {
                canonicalCode = statusCanonicalCode.ToString();
                CanonicalCodeToStringCache.TryAdd(statusCanonicalCode, canonicalCode);
            }

            return canonicalCode;
        }

        /// <summary>
        /// Helper method that populates span properties from http status code according
        /// to https://github.com/open-telemetry/opentelemetry-specification/blob/2316771e7e0ca3bfe9b2286d13e3a41ded6b8858/specification/data-http.md.
        /// </summary>
        /// <param name="httpStatusCode">Http status code.</param>
        /// <returns>Resolved span <see cref="Status"/> for the Http status code.</returns>
        public static Status ResolveSpanStatusForHttpStatusCode(int httpStatusCode)
        {
            var newStatus = Status.Unknown;

            if (httpStatusCode >= 200 && httpStatusCode <= 399)
            {
                newStatus = Status.Ok;
            }
            else if (httpStatusCode == 400)
            {
                newStatus = Status.InvalidArgument;
            }
            else if (httpStatusCode == 401)
            {
                newStatus = Status.Unauthenticated;
            }
            else if (httpStatusCode == 403)
            {
                newStatus = Status.PermissionDenied;
            }
            else if (httpStatusCode == 404)
            {
                newStatus = Status.NotFound;
            }
            else if (httpStatusCode == 429)
            {
                newStatus = Status.ResourceExhausted;
            }
            else if (httpStatusCode == 501)
            {
                newStatus = Status.Unimplemented;
            }
            else if (httpStatusCode == 503)
            {
                newStatus = Status.Unavailable;
            }
            else if (httpStatusCode == 504)
            {
                newStatus = Status.DeadlineExceeded;
            }

            return newStatus;
        }
    }
}
