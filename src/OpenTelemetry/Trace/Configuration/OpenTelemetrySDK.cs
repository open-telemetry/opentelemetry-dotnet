// <copyright file="OpenTelemetrySDK.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Trace.Configuration
{
    public class OpenTelemetrySDK
    {
        static OpenTelemetrySDK()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        public static void EnableOpenTelemetry(string source, ActivityProcessor activityProcessor)
        {
            ActivityListener listener = new ActivityListener
            {
                ActivityStarted = activityProcessor.OnStart,
                ActivityStopped = activityProcessor.OnEnd,
                ShouldListenTo = (activitySource) => activitySource.Name.Equals(source),
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }
    }
}
