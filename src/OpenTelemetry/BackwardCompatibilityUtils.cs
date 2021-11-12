// <copyright file="BackwardCompatibilityUtils.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace OpenTelemetry
{
    internal static class BackwardCompatibilityUtils
    {
        internal const string UnsetStatusCodeTagValue = "UNSET";
        internal const string OkStatusCodeTagValue = "OK";
        internal const string ErrorStatusCodeTagValue = "ERROR";

        internal static void SetActivityStatusUsingTags(Activity activity)
        {
            ActivityStatusCode statusCode = ActivityStatusCode.Unset;
            string statusDescription = null;
            foreach (var tag in activity.TagObjects)
            {
                if (tag.Key == SpanAttributeConstants.StatusCodeKey)
                {
                    statusCode = GetActivityStatusCode((string)tag.Value);
                }

                if (tag.Key == SpanAttributeConstants.StatusDescriptionKey)
                {
                    statusDescription = (string)tag.Value;
                }
            }

            if (statusCode != ActivityStatusCode.Unset)
            {
                activity.SetStatus(statusCode, statusDescription);
            }
        }

        private static ActivityStatusCode GetActivityStatusCode(string statusCodeTagValue)
        {
            return statusCodeTagValue switch
            {
                /*
                 * Note: Order here does matter for perf. Unset is
                 * first because assumption is most spans will be
                 * Unset, then Error. Ok is not set by the SDK.
                 */
                string _ when UnsetStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => ActivityStatusCode.Unset,
                string _ when ErrorStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => ActivityStatusCode.Error,
                string _ when OkStatusCodeTagValue.Equals(statusCodeTagValue, StringComparison.OrdinalIgnoreCase) => ActivityStatusCode.Ok,
                _ => ActivityStatusCode.Unset,
            };
        }
    }
}
