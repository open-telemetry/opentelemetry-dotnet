// <copyright file="BackwardCompatibilityHelper.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry
{
    internal static class BackwardCompatibilityHelper
    {
        internal static void SetActivityStatusUsingTags(Activity activity)
        {
            var tagState = default(TagEnumerationState);

            activity.EnumerateTags(ref tagState);

            if (tagState.StatusCode != ActivityStatusCode.Unset)
            {
                activity.SetStatus(tagState.StatusCode, tagState.StatusDescription);
            }
        }

        internal struct TagEnumerationState : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public ActivityStatusCode StatusCode { get; set; }

            public string StatusDescription { get; set; }

            public bool ForEach(KeyValuePair<string, object> activityTag)
            {
                if (activityTag.Value == null)
                {
                    return true;
                }

                string key = activityTag.Key;

                if (activityTag.Value is string strVal)
                {
                    if (key == SpanAttributeConstants.StatusCodeKey)
                    {
                        this.StatusCode = StatusHelper.GetActivityStatusCodeForTagValue(strVal);
                        return true;
                    }
                    else if (key == SpanAttributeConstants.StatusDescriptionKey)
                    {
                        this.StatusDescription = strVal;
                        return true;
                    }
                }

                return true;
            }
        }
    }
}
