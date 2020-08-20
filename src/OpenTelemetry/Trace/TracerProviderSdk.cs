// <copyright file="TracerProviderSdk.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    internal class TracerProviderSdk : TracerProvider
    {
        private readonly List<object> instrumentations = new List<object>();
        private readonly ActivityListener listener;
        private readonly Resource resource;
        private readonly Sampler sampler;
        private ActivityProcessor processor;
        private ActivitySourceAdapter adapter;

        static TracerProviderSdk()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        internal TracerProviderSdk(
            Resource resource,
            IEnumerable<string> sources,
            IEnumerable<TracerProviderBuilder.InstrumentationFactory> instrumentationFactories,
            Sampler sampler,
            List<ActivityProcessor> processors)
        {
            this.resource = resource;
            this.sampler = sampler;

            foreach (var processor in processors)
            {
                this.AddProcessor(processor);
            }

            if (instrumentationFactories.Any())
            {
                this.adapter = new ActivitySourceAdapter(sampler, this.processor, resource);
                foreach (var instrumentationFactory in instrumentationFactories)
                {
                    this.instrumentations.Add(instrumentationFactory.Factory(this.adapter));
                }
            }

            var listener = new ActivityListener
            {
                // Callback when Activity is started.
                ActivityStarted = (activity) =>
                {
                    if (activity.IsAllDataRequested)
                    {
                        activity.SetResource(this.resource);
                        this.processor?.OnStart(activity);
                    }
                },

                // Callback when Activity is stopped.
                ActivityStopped = (activity) =>
                {
                    if (activity.IsAllDataRequested)
                    {
                        this.processor?.OnEnd(activity);
                    }
                },

                // Setting this to true means TraceId will be always
                // available in sampling callbacks and will be the actual
                // traceid used, if activity ends up getting created.
                AutoGenerateRootContextTraceId = true,
            };

            if (sampler is AlwaysOnSampler)
            {
                listener.GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? ActivityDataRequest.AllDataAndRecorded : ActivityDataRequest.None;
            }
            else if (sampler is AlwaysOffSampler)
            {
                /*TODO: Change options.Parent.SpanId to options.Parent.TraceId
                        once AutoGenerateRootContextTraceId is removed.*/
                listener.GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? PropagateOrIgnoreData(options.Parent.SpanId) : ActivityDataRequest.None;
            }
            else
            {
                // This delegate informs ActivitySource about sampling decision when the parent context is an ActivityContext.
                listener.GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? ComputeActivityDataRequest(options, sampler) : ActivityDataRequest.None;
            }

            if (sources.Any())
            {
                // Sources can be null. This happens when user
                // is only interested in InstrumentationLibraries
                // which do not depend on ActivitySources.

                var wildcardMode = false;

                // Validation of source name is already done in builder.
                foreach (var name in sources)
                {
                    if (name.Contains('*'))
                    {
                        wildcardMode = true;
                    }
                }

                if (wildcardMode)
                {
                    var pattern = "^(" + string.Join("|", from name in sources select '(' + Regex.Escape(name).Replace("\\*", ".*") + ')') + ")$";
                    var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

                    // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                    // or not.
                    listener.ShouldListenTo = (activitySource) => regex.IsMatch(activitySource.Name);
                }
                else
                {
                    var activitySources = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                    foreach (var name in sources)
                    {
                        activitySources[name] = true;
                    }

                    // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                    // or not.
                    listener.ShouldListenTo = (activitySource) => activitySources.ContainsKey(activitySource.Name);
                }
            }

            ActivitySource.AddActivityListener(listener);
            this.listener = listener;
        }

        internal TracerProviderSdk AddProcessor(ActivityProcessor processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            if (this.processor == null)
            {
                this.processor = processor;
            }
            else if (this.processor is CompositeActivityProcessor compositeProcessor)
            {
                compositeProcessor.AddProcessor(processor);
            }
            else
            {
                this.processor = new CompositeActivityProcessor(new[]
                {
                    this.processor,
                    processor,
                });
            }

            this.adapter?.UpdateProcessor(this.processor);

            return this;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.instrumentations != null)
            {
                foreach (var item in this.instrumentations)
                {
                    (item as IDisposable)?.Dispose();
                }

                this.instrumentations.Clear();
            }

            (this.sampler as IDisposable)?.Dispose();
            this.processor?.Dispose();

            // Shutdown the listener last so that anything created while instrumentation cleans up will still be processed.
            // Redis instrumentation, for example, flushes during dispose which creates Activity objects for any profiling
            // sessions that were open.
            this.listener?.Dispose();

            base.Dispose(disposing);
        }

        private static ActivityDataRequest ComputeActivityDataRequest(
            in ActivityCreationOptions<ActivityContext> options,
            Sampler sampler)
        {
            // As we set ActivityListener.AutoGenerateRootContextTraceId = true,
            // Parent.TraceId will always be the TraceId of the to-be-created Activity,
            // if it get created.
            ActivityTraceId traceId = options.Parent.TraceId;

            var samplingParameters = new SamplingParameters(
                options.Parent,
                traceId,
                options.Name,
                options.Kind,
                options.Tags,
                options.Links);

            var shouldSample = sampler.ShouldSample(samplingParameters);

            var activityDataRequest = shouldSample.Decision switch
            {
                SamplingDecision.RecordAndSampled => ActivityDataRequest.AllDataAndRecorded,
                SamplingDecision.Record => ActivityDataRequest.AllData,
                _ => ActivityDataRequest.PropagationData
            };

            if (activityDataRequest != ActivityDataRequest.PropagationData)
            {
                return activityDataRequest;
            }

            /*TODO: Change options.Parent.SpanId to options.Parent.TraceId
                    once AutoGenerateRootContextTraceId is removed.*/
            return PropagateOrIgnoreData(options.Parent.SpanId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActivityDataRequest PropagateOrIgnoreData(ActivitySpanId spanId)
        {
            var isRootSpan = spanId == default;

            // If it is the root span select PropagationData so the trace ID is preserved
            // even if no activity of the trace is recorded (sampled per OpenTelemetry parlance).
            return isRootSpan
                ? ActivityDataRequest.PropagationData
                : ActivityDataRequest.None;
        }
    }
}
