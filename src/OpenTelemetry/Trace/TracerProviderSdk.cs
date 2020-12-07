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
using System.Threading;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    internal class TracerProviderSdk : TracerProvider
    {
        internal int ShutdownCount;

        private readonly List<object> instrumentations = new List<object>();
        private readonly ActivityListener listener;
        private readonly Sampler sampler;
        private readonly ActivitySourceAdapter adapter;
        private BaseProcessor<Activity> processor;

        internal TracerProviderSdk(
            Resource resource,
            IEnumerable<TraceVersion> sources,
            IEnumerable<TracerProviderBuilderSdk.DiagnosticSourceInstrumentationFactory> diagnosticSourceInstrumentationFactories,
            IEnumerable<TracerProviderBuilderSdk.InstrumentationFactory> instrumentationFactories,
            Sampler sampler,
            List<BaseProcessor<Activity>> processors)
        {
            this.Resource = resource;
            this.sampler = sampler;

            foreach (var processor in processors)
            {
                this.AddProcessor(processor);
            }

            if (diagnosticSourceInstrumentationFactories.Any())
            {
                this.adapter = new ActivitySourceAdapter(sampler, this.processor);
                foreach (var instrumentationFactory in diagnosticSourceInstrumentationFactories)
                {
                    this.instrumentations.Add(instrumentationFactory.Factory(this.adapter));
                }
            }

            if (instrumentationFactories.Any())
            {
                foreach (var instrumentationFactory in instrumentationFactories)
                {
                    this.instrumentations.Add(instrumentationFactory.Factory());
                }
            }

            var listener = new ActivityListener
            {
                // Callback when Activity is started.
                ActivityStarted = (activity) =>
                {
                    OpenTelemetrySdkEventSource.Log.ActivityStarted(activity);

                    if (!activity.IsAllDataRequested)
                    {
                        return;
                    }

                    if (SuppressInstrumentationScope.IncrementIfTriggered() == 0)
                    {
                        this.processor?.OnStart(activity);
                    }
                },

                // Callback when Activity is stopped.
                ActivityStopped = (activity) =>
                {
                    OpenTelemetrySdkEventSource.Log.ActivityStopped(activity);

                    if (!activity.IsAllDataRequested)
                    {
                        return;
                    }

                    if (SuppressInstrumentationScope.DecrementIfTriggered() == 0)
                    {
                        this.processor?.OnEnd(activity);
                    }
                },
            };

            if (sampler is AlwaysOnSampler)
            {
                listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None;
            }
            else if (sampler is AlwaysOffSampler)
            {
                listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? PropagateOrIgnoreData(options.Parent.TraceId) : ActivitySamplingResult.None;
            }
            else
            {
                // This delegate informs ActivitySource about sampling decision when the parent context is an ActivityContext.
                listener.Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                    !Sdk.SuppressInstrumentation ? ComputeActivitySamplingResult(options, sampler) : ActivitySamplingResult.None;
            }

            if (sources.Any())
            {
                foreach (var traceVersion in sources)
                {
                    listener.ShouldListenTo =
                        (activitySource) => VersionHelper.Compare(activitySource.Version, traceVersion.MinVersion, traceVersion.MaxVersion);
                }
            }

            ActivitySource.AddActivityListener(listener);
            this.listener = listener;
        }

        internal Resource Resource { get; }

        internal TracerProviderSdk AddProcessor(BaseProcessor<Activity> processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            processor.SetParentProvider(this);

            if (this.processor == null)
            {
                this.processor = processor;
            }
            else if (this.processor is CompositeProcessor<Activity> compositeProcessor)
            {
                compositeProcessor.AddProcessor(processor);
            }
            else
            {
                this.processor = new CompositeProcessor<Activity>(new[]
                {
                    this.processor,
                    processor,
                });
            }

            this.adapter?.UpdateProcessor(this.processor);

            return this;
        }

        /// <summary>
        /// Called by <c>Shutdown</c>. This function should block the current
        /// thread until shutdown completed or timed out.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> when shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This function is called synchronously on the thread which made the
        /// first call to <c>Shutdown</c>. This function should not throw
        /// exceptions.
        /// </remarks>
        internal bool OnShutdown(int timeoutMilliseconds)
        {
            // TO DO Put OnShutdown logic in a task to run within the user provider timeOutMilliseconds
            bool? result;
            if (this.instrumentations != null)
            {
                foreach (var item in this.instrumentations)
                {
                    (item as IDisposable)?.Dispose();
                }

                this.instrumentations.Clear();
            }

            result = this.processor?.Shutdown(timeoutMilliseconds);
            this.listener?.Dispose();
            return result ?? true;
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

            // Wait for up to 5 seconds grace period
            this.processor?.Shutdown(5000);
            this.processor?.Dispose();

            // Shutdown the listener last so that anything created while instrumentation cleans up will still be processed.
            // Redis instrumentation, for example, flushes during dispose which creates Activity objects for any profiling
            // sessions that were open.
            this.listener?.Dispose();

            base.Dispose(disposing);
        }

        private static ActivitySamplingResult ComputeActivitySamplingResult(
            in ActivityCreationOptions<ActivityContext> options,
            Sampler sampler)
        {
            var samplingParameters = new SamplingParameters(
                options.Parent,
                options.TraceId,
                options.Name,
                options.Kind,
                options.Tags,
                options.Links);

            var shouldSample = sampler.ShouldSample(samplingParameters);

            var activitySamplingResult = shouldSample.Decision switch
            {
                SamplingDecision.RecordAndSample => ActivitySamplingResult.AllDataAndRecorded,
                SamplingDecision.RecordOnly => ActivitySamplingResult.AllData,
                _ => ActivitySamplingResult.PropagationData
            };

            if (activitySamplingResult != ActivitySamplingResult.PropagationData)
            {
                foreach (var att in shouldSample.Attributes)
                {
                    options.SamplingTags.Add(att.Key, att.Value);
                }

                return activitySamplingResult;
            }

            return PropagateOrIgnoreData(options.Parent.TraceId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ActivitySamplingResult PropagateOrIgnoreData(ActivityTraceId traceId)
        {
            var isRootSpan = traceId == default;

            // If it is the root span select PropagationData so the trace ID is preserved
            // even if no activity of the trace is recorded (sampled per OpenTelemetry parlance).
            return isRootSpan
                ? ActivitySamplingResult.PropagationData
                : ActivitySamplingResult.None;
        }
    }
}
