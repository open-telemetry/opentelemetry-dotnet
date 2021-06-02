// <copyright file="TracerProviderBuilderBase.cs" company="OpenTelemetry Authors">
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
    public abstract class TracerProviderBuilderBase : TracerProviderBuilder
    {
        private readonly List<InstrumentationFactory> instrumentationFactories = new List<InstrumentationFactory>();
        private readonly List<BaseProcessor<Activity>> processors = new List<BaseProcessor<Activity>>();
        private readonly List<string> sources = new List<string>();
        private readonly Dictionary<string, bool> legacyActivityOperationNames = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault();
        private Sampler sampler = new ParentBasedSampler(new AlwaysOnSampler());

        protected TracerProviderBuilderBase()
        {
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override TracerProviderBuilder AddLegacySource(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException($"{nameof(operationName)} contains null or whitespace string.");
            }

            this.legacyActivityOperationNames[operationName] = true;

            return this;
        }

        /// <summary>
        /// Sets whether the status of <see cref="System.Diagnostics.Activity"/>
        /// should be set to <c>Status.Error</c> when it ended abnormally due to an unhandled exception.
        /// </summary>
        /// <param name="enabled">Enabled or not.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder SetErrorStatusOnException(bool enabled)
        {
            ExceptionProcessor existingExceptionProcessor = null;

            if (this.processors.Count > 0)
            {
                existingExceptionProcessor = this.processors[0] as ExceptionProcessor;
            }

            if (enabled)
            {
                if (existingExceptionProcessor == null)
                {
                    try
                    {
                        this.processors.Insert(0, new ExceptionProcessor());
                    }
                    catch (Exception ex)
                    {
                        throw new NotSupportedException("SetErrorStatusOnException is not supported on this platform.", ex);
                    }
                }
            }
            else
            {
                if (existingExceptionProcessor != null)
                {
                    this.processors.RemoveAt(0);
                    existingExceptionProcessor.Dispose();
                }
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
        /// Adds instrumentation to the provider.
        /// </summary>
        /// <param name="instrumentationName">Instrumentation name.</param>
        /// <param name="instrumentationVersion">Instrumentation version.</param>
        /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        protected TracerProviderBuilder AddInstrumentation(
            string instrumentationName,
            string instrumentationVersion,
            Func<object> instrumentationFactory)
        {
            this.instrumentationFactories.Add(
                new InstrumentationFactory(instrumentationName, instrumentationVersion, instrumentationFactory));

            return this;
        }

        /// <summary>
        /// Run the configured actions to initialize the <see cref="TracerProvider"/>.
        /// </summary>
        /// <returns><see cref="TracerProvider"/>.</returns>
        protected TracerProvider Build()
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
