// <copyright file="OpenTelemetrySdk.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace.Samplers;

namespace OpenTelemetry.Trace.Configuration
{
    public class OpenTelemetrySdk
    {
        static OpenTelemetrySdk()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        /// <summary>
        /// Enables OpenTelemetry.
        /// </summary>
        /// <param name="configureOpenTelemetryBuilder">Function that configures OpenTelemetryBuilder.</param>
        /// <returns><see cref="IDisposable"/> to be disposed on application shutdown.</returns>
        /// <remarks>
        /// Basic implementation only. Most logic from TracerBuilder will be ported here.
        /// </remarks>
        public static IDisposable EnableOpenTelemetry(Action<OpenTelemetryBuilder> configureOpenTelemetryBuilder)
        {
            var openTelemetryBuilder = new OpenTelemetryBuilder();
            configureOpenTelemetryBuilder(openTelemetryBuilder);

            ActivitySampler sampler = openTelemetryBuilder.Sampler ?? new AlwaysOnActivitySampler();

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

            // This is what subscribes to Activities.
            // Think of this as the replacement for DiagnosticListener.AllListeners.Subscribe(onNext => diagnosticListener.Subscribe(..));
            ActivityListener listener = new ActivityListener
            {
                // Callback when Activity is started.
                ActivityStarted = activityProcessor.OnStart,

                // Callback when Activity is started.
                ActivityStopped = activityProcessor.OnEnd,

                // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                // or not
                ShouldListenTo = (activitySource) => openTelemetryBuilder.ActivitySourceNames?.Contains(activitySource.Name.ToUpperInvariant()) ?? false,

                // The following parameter is not used now.
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,

                // This delegate informs ActivitySource about sampling decision.
                // Following simple behavior is enabled now:
                // If Sampler returns IsSampled as true, returns ActivityDataRequest.AllDataAndRecorded
                // This creates Activity and sets its IsAllDataRequested to true.
                // Library authors can check activity.IsAllDataRequested and avoid
                // doing any additional telemetry population.
                // Activity.IsAllDataRequested is the equivalent of Span.IsRecording
                //
                // If Sampler returns IsSampled as false, returns ActivityDataRequest.None
                // This prevents Activity from being created at all.
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) =>
                {
                    var shouldSample = sampler.ShouldSample(
                        options.Parent,
                        options.Parent.TraceId,
                        spanId: default, // Passing default SpanId here. The actual SpanId is not known before actual Activity creation
                        options.Name,
                        options.Kind,
                        options.Tags,
                        options.Links);
                    if (shouldSample.IsSampled)
                    {
                        return ActivityDataRequest.AllDataAndRecorded;
                    }
                    else
                    {
                        return ActivityDataRequest.None;
                    }

                    // TODO: Improve this to properly use ActivityDataRequest.AllData, PropagationData as well.
                },
            };

            ActivitySource.AddActivityListener(listener);

            return listener;
        }
    }
}
