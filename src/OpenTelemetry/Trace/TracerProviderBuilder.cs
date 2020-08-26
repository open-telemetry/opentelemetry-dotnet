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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Build TracerProvider with Resource, Sampler, Processors and Instrumentation.
    /// </summary>
    public class TracerProviderBuilder
    {
        private readonly List<InstrumentationFactory> instrumentationFactories = new List<InstrumentationFactory>();
        private readonly List<ActivityProcessor> processors = new List<ActivityProcessor>();
        private readonly List<string> sources = new List<string>();
        private Resource resource = Resource.Empty;
        private Sampler sampler = new ParentBasedSampler(new AlwaysOnSampler());

        internal TracerProviderBuilder()
        {
        }

        /// <summary>
        /// Sets sampler.
        /// </summary>
        /// <param name="sampler">Sampler instance.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder SetSampler(Sampler sampler)
        {
            if (sampler == null)
            {
                throw new ArgumentNullException(nameof(sampler));
            }

            this.sampler = sampler;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="Resource"/> describing the app associated with all traces. Overwrites currently set resource.
        /// </summary>
        /// <param name="resource">Resource to be associate with all traces.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder SetResource(Resource resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            this.resource = resource;
            return this;
        }

        /// <summary>
        /// Adds given activitysource names to the list of subscribed sources.
        /// </summary>
        /// <param name="names">Activity source names.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder AddSource(params string[] names)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException($"{nameof(names)} contains null or whitespace string.");
                }

                // TODO: We need to fix the listening model.
                // Today it ignores version.
                this.sources.Add(name);
            }

            return this;
        }

        /// <summary>
        /// Adds processor to the provider.
        /// </summary>
        /// <param name="processor">Activity processor to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public TracerProviderBuilder AddProcessor(ActivityProcessor processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            this.processors.Add(processor);

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

            this.instrumentationFactories.Add(
                new InstrumentationFactory(
                    typeof(TInstrumentation).Name,
                    "semver:" + typeof(TInstrumentation).Assembly.GetName().Version,
                    instrumentationFactory));

            return this;
        }

        public TracerProvider Build()
        {
            return new TracerProviderSdk(this.resource, this.sources, this.instrumentationFactories, this.sampler, this.processors);
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
