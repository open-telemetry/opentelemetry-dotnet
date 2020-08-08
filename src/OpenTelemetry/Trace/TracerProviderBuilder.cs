// <copyright file="TracerProviderBuilder.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Samplers;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Build TracerProvider with Resource, Sampler, Processors and Instrumentation.
    /// </summary>
    public class TracerProviderBuilder
    {
        internal TracerProviderBuilder()
        {
        }

        internal Sampler Sampler { get; private set; }

        internal Resource Resource { get; private set; } = Resource.Empty;

        internal ActivityProcessor ActivityProcessor { get; private set; }

        internal Dictionary<string, bool> ActivitySourceNames { get; private set; }

        internal List<InstrumentationFactory> InstrumentationFactories { get; private set; }

        /// <summary>
        /// Sets sampler.
        /// </summary>
        /// <param name="sampler">Sampler instance.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder SetSampler(Sampler sampler)
        {
            this.Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Resource"/> describing the app associated with all traces. Overwrites currently set resource.
        /// </summary>
        /// <param name="resource">Resource to be associate with all traces.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder SetResource(Resource resource)
        {
            this.Resource = resource ?? Resource.Empty;
            return this;
        }

        /// <summary>
        /// Adds given activitysource name to the list of subscribed sources.
        /// </summary>
        /// <param name="activitySourceName">Activity source name.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder AddActivitySource(string activitySourceName)
        {
            // TODO: We need to fix the listening model.
            // Today it ignores version.
            if (this.ActivitySourceNames == null)
            {
                this.ActivitySourceNames = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }

            this.ActivitySourceNames[activitySourceName] = true;
            return this;
        }

        /// <summary>
        /// Adds given activitysource names to the list of subscribed sources.
        /// </summary>
        /// <param name="activitySourceNames">Activity source names.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder AddActivitySources(IEnumerable<string> activitySourceNames)
        {
            // TODO: We need to fix the listening model.
            // Today it ignores version.
            if (this.ActivitySourceNames == null)
            {
                this.ActivitySourceNames = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var activitySourceName in activitySourceNames)
            {
                this.ActivitySourceNames[activitySourceName] = true;
            }

            return this;
        }

        /// <summary>
        /// Adds processor to the provider.
        /// </summary>
        /// <param name="activityProcessor">Activity processor to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder AddProcessor(ActivityProcessor activityProcessor)
        {
            if (this.ActivityProcessor == null)
            {
                this.ActivityProcessor = activityProcessor;
            }
            else if (this.ActivityProcessor is CompositeActivityProcessor compositeProcessor)
            {
                compositeProcessor.AddProcessor(activityProcessor);
            }
            else
            {
                this.ActivityProcessor = new CompositeActivityProcessor(new[]
                {
                    this.ActivityProcessor,
                    activityProcessor,
                });
            }

            return this;
        }

        /// <summary>
        /// Adds auto-instrumentations for activity.
        /// </summary>
        /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
        /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder AddInstrumentation<TInstrumentation>(
            Func<ActivitySourceAdapter, TInstrumentation> instrumentationFactory)
            where TInstrumentation : class
        {
            if (instrumentationFactory == null)
            {
                throw new ArgumentNullException(nameof(instrumentationFactory));
            }

            if (this.InstrumentationFactories == null)
            {
                this.InstrumentationFactories = new List<InstrumentationFactory>();
            }

            this.InstrumentationFactories.Add(
                new InstrumentationFactory(
                    typeof(TInstrumentation).Name,
                    "semver:" + typeof(TInstrumentation).Assembly.GetName().Version,
                    instrumentationFactory));

            return this;
        }

        public TracerProvider Build()
        {
            this.Sampler = this.Sampler ?? new ParentOrElseSampler(new AlwaysOnSampler());

            var provider = new TracerProviderSdk
            {
                Resource = this.Resource,
                Sampler = this.Sampler,
                ActivityProcessor = this.ActivityProcessor,
            };

            var activitySource = new ActivitySourceAdapter(provider.Sampler, provider.ActivityProcessor, provider.Resource);

            if (this.InstrumentationFactories != null)
            {
                foreach (var instrumentation in this.InstrumentationFactories)
                {
                    provider.Instrumentations.Add(instrumentation.Factory(activitySource));
                }
            }

            provider.ActivityListener = new ActivityListener
            {
                // Callback when Activity is started.
                ActivityStarted = (activity) =>
                {
                    if (activity.IsAllDataRequested)
                    {
                        activity.SetResource(this.Resource);
                    }

                    provider.ActivityProcessor?.OnStart(activity);
                },

                // Callback when Activity is stopped.
                ActivityStopped = (activity) =>
                {
                    provider.ActivityProcessor?.OnEnd(activity);
                },

                // Function which takes ActivitySource and returns true/false to indicate if it should be subscribed to
                // or not.
                ShouldListenTo = (activitySource) =>
                {
                    if (this.ActivitySourceNames == null)
                    {
                        return false;
                    }

                    return this.ActivitySourceNames.ContainsKey(activitySource.Name);
                },

                // Setting this to true means TraceId will be always
                // available in sampling callbacks and will be the actual
                // traceid used, if activity ends up getting created.
                AutoGenerateRootContextTraceId = true,

                // This delegate informs ActivitySource about sampling decision when the parent context is an ActivityContext.
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ComputeActivityDataRequest(options, this.Sampler),
            };

            ActivitySource.AddActivityListener(provider.ActivityListener);

            return provider;
        }

        internal static ActivityDataRequest ComputeActivityDataRequest(
            in ActivityCreationOptions<ActivityContext> options,
            Sampler sampler)
        {
            var isRootSpan = /*TODO: Put back once AutoGenerateRootContextTraceId is removed.
                              options.Parent.TraceId == default ||*/ options.Parent.SpanId == default;

            if (sampler != null)
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
                if (shouldSample.IsSampled)
                {
                    return ActivityDataRequest.AllDataAndRecorded;
                }
            }

            // If it is the root span select PropagationData so the trace ID is preserved
            // even if no activity of the trace is recorded (sampled per OpenTelemetry parlance).
            return isRootSpan
                ? ActivityDataRequest.PropagationData
                : ActivityDataRequest.None;
        }

        internal readonly struct InstrumentationFactory
        {
            public readonly string Name;
            public readonly string Version;
            public readonly Func<ActivitySourceAdapter, object> Factory;

            internal InstrumentationFactory(string name, string version, Func<ActivitySourceAdapter, object> factory)
            {
                this.Name = name;
                this.Version = version;
                this.Factory = factory;
            }
        }
    }
}
