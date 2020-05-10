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

using System;
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

        /// <summary>
        /// Enables OpenTelemetry.
        /// </summary>
        /// <param name="configureOpenTelemetryBuilder">Function that configures OpenTelemetryBuilder.</param>
        /// <remarks>
        /// Basic implementation only. Most logic from TracerBuilder will be ported here.
        /// </remarks>
        public static void EnableOpenTelemetry(Action<OpenTelemetryBuilder> configureOpenTelemetryBuilder)
        {
            var openTelemetryBuilder = new OpenTelemetryBuilder();
            configureOpenTelemetryBuilder(openTelemetryBuilder);

            ActivityProcessor activityProcessor;
            if (openTelemetryBuilder.ProcessingPipeline == null)
            {
                // if there are no pipelines are configured, use noop processor
                activityProcessor = new NoopActivityProcessor();
            }
            else
            {
                activityProcessor = openTelemetryBuilder.ProcessingPipeline.Build();
            }

            ActivityListener listener = new ActivityListener
            {
                ActivityStarted = activityProcessor.OnStart,
                ActivityStopped = activityProcessor.OnEnd,
                ShouldListenTo = (activitySource) => openTelemetryBuilder.ActivitySourceNames.Contains(activitySource.Name.ToLowerInvariant()),

                // The following parameters are not used now.
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }
    }
}
