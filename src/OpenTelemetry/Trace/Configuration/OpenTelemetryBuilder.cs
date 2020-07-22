// <copyright file="OpenTelemetryBuilder.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace.Configuration
{
    /// <summary>
    /// Build OpenTelemetry pipeline.
    /// Currently supports a single processing pipeline, and any number of activity source names
    /// to subscribe to.
    /// </summary>
    public class OpenTelemetryBuilder
    {
        internal OpenTelemetryBuilder()
        {
        }

        internal List<ActivityProcessorPipelineBuilder> ProcessingPipelines { get; private set; }

        internal List<InstrumentationFactory> InstrumentationFactories { get; private set; }

        internal Sampler Sampler { get; private set; }

        internal Resource Resource { get; private set; } = Resource.Empty;

        internal HashSet<string> ActivitySourceNames { get; private set; }

        /// <summary>
        /// Sets processing and exporting pipeline.
        /// </summary>
        /// <param name="configure">Function that configures pipeline.</param>
        /// <returns>Returns <see cref="OpenTelemetryBuilder"/> for chaining.</returns>
        public OpenTelemetryBuilder AddProcessorPipeline(Action<ActivityProcessorPipelineBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            if (this.ProcessingPipelines == null)
            {
                this.ProcessingPipelines = new List<ActivityProcessorPipelineBuilder>();
            }

            var pipelineBuilder = new ActivityProcessorPipelineBuilder();
            configure(pipelineBuilder);
            this.ProcessingPipelines.Add(pipelineBuilder);
            return this;
        }

        /// <summary>
        /// Configures sampler.
        /// </summary>
        /// <param name="sampler">Sampler instance.</param>
        /// <returns>Returns <see cref="OpenTelemetryBuilder"/> for chaining.</returns>
        public OpenTelemetryBuilder SetSampler(Sampler sampler)
        {
            this.Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Resource"/> describing the app associated with all traces. Overwrites currently set resource.
        /// </summary>
        /// <param name="resource">Resource to be associate with all traces.</param>
        /// <returns>Returns <see cref="OpenTelemetryBuilder"/> for chaining.</returns>
        public OpenTelemetryBuilder SetResource(Resource resource)
        {
            this.Resource = resource ?? Resource.Empty;
            return this;
        }

        /// <summary>
        /// Adds given activitysource name to the list of subscribed sources.
        /// </summary>
        /// <param name="activitySourceName">Activity source name.</param>
        /// <returns>Returns <see cref="OpenTelemetryBuilder"/> for chaining.</returns>
        public OpenTelemetryBuilder AddActivitySource(string activitySourceName)
        {
            if (this.ActivitySourceNames == null)
            {
                this.ActivitySourceNames = new HashSet<string>();
            }

            this.ActivitySourceNames.Add(activitySourceName.ToUpperInvariant());
            return this;
        }

        /// <summary>
        /// Adds auto-instrumentations for activity.
        /// </summary>
        /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
        /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
        /// <returns>Returns <see cref="OpenTelemetryBuilder"/> for chaining.</returns>
        public OpenTelemetryBuilder AddInstrumentation<TInstrumentation>(
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
