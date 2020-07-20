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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Export.Internal;
using OpenTelemetry.Trace.Samplers;

namespace OpenTelemetry.Trace.Configuration
{
    public class OpenTelemetrySdk : IDisposable
    {
        private readonly List<object> instrumentations = new List<object>();
        private Resource resource;
        private ActivityProcessor activityProcessor;
        private ActivityListener listener;

        static OpenTelemetrySdk()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        private OpenTelemetrySdk()
        {
        }

        /// <summary>
        /// Enables OpenTelemetry.
        /// </summary>
        /// <param name="configureOpenTelemetryBuilder">Function that configures OpenTelemetryBuilder.</param>
        /// <returns><see cref="OpenTelemetrySdk"/> instance which can be disposed on application shutdown.</returns>
        /// <remarks>
        /// Basic implementation only. Most logic from TracerBuilder will be ported here.
        /// </remarks>
        public static OpenTelemetrySdk EnableOpenTelemetry(Action<OpenTelemetryBuilder> configureOpenTelemetryBuilder)
        {
            var openTelemetryBuilder = new OpenTelemetryBuilder();
            configureOpenTelemetryBuilder?.Invoke(openTelemetryBuilder);

            var openTelemetrySDK = new OpenTelemetrySdk();
            ActivitySampler sampler = openTelemetryBuilder.Sampler ?? new AlwaysOnActivitySampler();

            ActivityProcessor activityProcessor;
            if (openTelemetryBuilder.ProcessingPipelines == null || !openTelemetryBuilder.ProcessingPipelines.Any())
            {
                // if there are no pipelines are configured, use noop processor
                activityProcessor = new NoopActivityProcessor();
            }
            else if (openTelemetryBuilder.ProcessingPipelines.Count == 1)
            {
                // if there is only one pipeline - use it's outer processor as a
                // single processor on the tracerSdk.
                var processorFactory = openTelemetryBuilder.ProcessingPipelines[0];
                activityProcessor = processorFactory.Build();
            }
            else
            {
                // if there are more pipelines, use processor that will broadcast to all pipelines
                var processors = new ActivityProcessor[openTelemetryBuilder.ProcessingPipelines.Count];

                for (int i = 0; i < openTelemetryBuilder.ProcessingPipelines.Count; i++)
                {
                    processors[i] = openTelemetryBuilder.ProcessingPipelines[i].Build();
                }

                activityProcessor = new BroadcastActivityProcessor(processors);
            }

            openTelemetrySDK.resource = openTelemetryBuilder.Resource;

            var activitySource = new ActivitySourceAdapter(sampler, activityProcessor, openTelemetrySDK.resource);

            if (openTelemetryBuilder.InstrumentationFactories != null)
            {
                foreach (var instrumentation in openTelemetryBuilder.InstrumentationFactories)
                {
                    openTelemetrySDK.instrumentations.Add(instrumentation.Factory(activitySource));
                }
            }

            // This is what subscribes to Activities.
            // Think of this as the replacement for DiagnosticListener.AllListeners.Subscribe(onNext => diagnosticListener.Subscribe(..));
            openTelemetrySDK.listener = new ActivityListener
            {
                // Callback when Activity is started.
                ActivityStarted = (activity) =>
                {
                    if (activity.IsAllDataRequested)
                    {
                        activity.SetResource(openTelemetrySDK.resource);
                    }

                    activityProcessor.OnStart(activity);
                },

                // Callback when Activity is stopped.
                ActivityStopped = activityProcessor.OnEnd,

                // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                // or not
                ShouldListenTo = (activitySource) => openTelemetryBuilder.ActivitySourceNames?.Contains(activitySource.Name.ToUpperInvariant()) ?? false,

                // The following parameter is not used now.
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,

                // This delegate informs ActivitySource about sampling decision when the parent context is an ActivityContext.
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ComputeActivityDataRequest(options, sampler),
            };

            ActivitySource.AddActivityListener(openTelemetrySDK.listener);
            openTelemetrySDK.activityProcessor = activityProcessor;
            return openTelemetrySDK;
        }

        public void Dispose()
        {
            foreach (var item in this.instrumentations)
            {
                if (item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            this.instrumentations.Clear();

            if (this.activityProcessor is IDisposable disposableProcessor)
            {
                disposableProcessor.Dispose();
            }

            // Shutdown the listener last so that anything created while instrumentation cleans up will still be processed.
            // Redis instrumentation, for example, flushes during dispose which creates Activity objects for any profiling
            // sessions that were open.
            this.listener.Dispose();
        }

        internal static ActivityDataRequest ComputeActivityDataRequest(
            in ActivityCreationOptions<ActivityContext> options,
            ActivitySampler sampler)
        {
            var isRootSpan = options.Parent.TraceId == default;

            // This is not going to be the final traceId of the Activity (if one is created), however, it is
            // needed in order for the sampling to work. This differs from other OTel SDKs in which it is
            // the Sampler always receives the actual traceId of a root span/activity.
            ActivityTraceId traceId = !isRootSpan
                ? options.Parent.TraceId
                : ActivityTraceId.CreateRandom();

            var samplingParameters = new ActivitySamplingParameters(
                options.Parent,
                traceId,
                options.Name,
                options.Kind,
                options.Tags,
                options.Links);

            var shouldSample = sampler.ShouldSample(samplingParameters);
            if (shouldSample.IsSampled)
            {
                return ActivityDataRequest.AllDataAndRecorded;
            }

            // If it is the root span select PropagationData so the trace ID is preserved
            // even if no activity of the trace is recorded (sampled per OpenTelemetry parlance).
            return isRootSpan
                ? ActivityDataRequest.PropagationData
                : ActivityDataRequest.None;
        }
    }
}
