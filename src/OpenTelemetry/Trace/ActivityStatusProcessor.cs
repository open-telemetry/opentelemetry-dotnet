// <copyright file="ActivityStatusProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    internal sealed class ActivityStatusProcessor : BaseProcessor<Activity>
    {
        public const string UnsetStatusCodeTagValue = "UNSET";
        public const string OkStatusCodeTagValue = "OK";
        public const string ErrorStatusCodeTagValue = "ERROR";

        private ActivityStatusCode statusCode = ActivityStatusCode.Unset;
        private string activityStatusDescription;

        /// <inheritdoc />
        public override void OnEnd(Activity activity)
        {
            if (this.statusCode == ActivityStatusCode.Unset)
            {
                foreach (var tag in activity.TagObjects)
                {
                    if (tag.Key == SpanAttributeConstants.StatusCodeKey)
                    {
                        this.statusCode = this.GetActivityStatusCode((string)tag.Value);
                    }

                    if (tag.Key == SpanAttributeConstants.StatusDescriptionKey)
                    {
                        this.activityStatusDescription = (string)tag.Value;
                    }
                }

                if (this.statusCode != ActivityStatusCode.Unset)
                {
                    activity.SetStatus(this.statusCode, this.activityStatusDescription);
                }
            }
        }

        private ActivityStatusCode GetActivityStatusCode(string statusCodeTagValue)
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
