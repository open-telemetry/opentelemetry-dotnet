// <copyright file="TracerProviderBuilderSdk.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Build TracerProvider with Resource, Sampler, Processors and Instrumentation.
    /// </summary>
    internal class TracerProviderBuilderSdk : TracerProviderBuilder
    {
        private readonly List<InstrumentationFactory> instrumentationFactories = new List<InstrumentationFactory>();

        private readonly TracerProviderOptions options;
        private readonly List<BaseProcessor<Activity>> processors = new List<BaseProcessor<Activity>>();
        private readonly List<string> sources = new List<string>();
        private readonly Dictionary<string, bool> legacyActivityOperationNames = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault();
        private Sampler sampler = new ParentBasedSampler(new AlwaysOnSampler());

        internal TracerProviderBuilderSdk(TracerProviderOptions options)
        {
            this.options = options ?? new TracerProviderOptions();

            if (options.SetErrorStatusOnException)
            {
                try
                {
                    this.AddProcessor(new ExceptionProcessor());
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException($"{nameof(options.SetErrorStatusOnException)} is not supported on this platform.", ex);
                }
            }
        }

        /// <summary>
        /// Adds an instrumentation to the provider.
        /// </summary>
        /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
        /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(
            Func<TInstrumentation> instrumentationFactory)
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

        /// <summary>
        /// Adds given activitysource names to the list of subscribed sources.
        /// </summary>
        /// <param name="names">Activity source names.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public override TracerProviderBuilder AddSource(params string[] names)
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
        /// Sets sampler.
        /// </summary>
        /// <param name="sampler">Sampler instance.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder SetSampler(Sampler sampler)
        {
            this.sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// </summary>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            this.resourceBuilder = resourceBuilder ?? throw new ArgumentNullException(nameof(resourceBuilder));
            return this;
        }

        /// <summary>
        /// Adds processor to the provider.
        /// </summary>
        /// <param name="processor">Activity processor to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder AddProcessor(BaseProcessor<Activity> processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            this.processors.Add(processor);

            return this;
        }

        /// <summary>
        /// Adds activity with a given operation name to the list of subscribed activities. This is only for legacy activities (i.e. activities created without using ActivitySource API).
        /// </summary>
        /// <param name="operationName">OperationName to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder AddLegacyActivity(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException($"{nameof(operationName)} contains null or whitespace string.");
            }

            this.legacyActivityOperationNames[operationName] = true;

            return this;
        }

        internal TracerProvider Build()
        {
            return new TracerProviderSdk(
                this.resourceBuilder.Build(),
                this.sources,
                this.instrumentationFactories,
                this.sampler,
                this.processors,
                this.legacyActivityOperationNames);
        }

        internal readonly struct InstrumentationFactory
        {
            public readonly string Name;
            public readonly string Version;
            public readonly Func<object> Factory;

            internal InstrumentationFactory(string name, string version, Func<object> factory)
            {
                this.Name = name;
                this.Version = version;
                this.Factory = factory;
            }
        }
    }
}
