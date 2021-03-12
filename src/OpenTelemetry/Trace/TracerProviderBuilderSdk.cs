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
using System.Diagnostics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Build TracerProvider with Resource, Sampler, Processors and Instrumentation.
    /// </summary>
    public class TracerProviderBuilderSdk : TracerProviderBuilder
    {
        protected readonly TracerProviderSdkOptions options = new TracerProviderSdkOptions();

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

            this.options.InstrumentationFactories.Add(
                new TracerProviderSdkOptions.InstrumentationFactory(
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
                this.options.Sources.Add(name);
            }

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
            this.options.SetErrorStatusOnException = enabled;

            return this;
        }

        /// <summary>
        /// Sets sampler.
        /// </summary>
        /// <param name="samplerFactory">Function that builds a <see cref="Sampler"/> instance.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder SetSampler(Func<Sampler> samplerFactory)
        {
            if (samplerFactory == null)
            {
                throw new ArgumentNullException(nameof(samplerFactory));
            }

            this.options.SamplerFactory = samplerFactory;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// </summary>
        /// <param name="resourceFactory">Function that builds a <see cref="Resource"/> instance.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder SetResource(Func<Resource> resourceFactory)
        {
            if (resourceFactory == null)
            {
                throw new ArgumentNullException(nameof(resourceFactory));
            }

            this.options.ResourceFactory = resourceFactory;
            return this;
        }

        /// <summary>
        /// Adds processor to the provider.
        /// </summary>
        /// <param name="processorFactory">Function that builds a <see cref="BaseProcessor{Activity}"/> instance to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder AddProcessor(Func<BaseProcessor<Activity>> processorFactory)
        {
            if (processorFactory == null)
            {
                throw new ArgumentNullException(nameof(processorFactory));
            }

            this.options.ProcessorFactories.Add(processorFactory);

            return this;
        }

        /// <summary>
        /// Adds a listener for <see cref="Activity"/> objects created with the given operation name to the <see cref="TracerProviderBuilder"/>.
        /// </summary>
        /// <remarks>
        /// This is provided to capture legacy <see cref="Activity"/> objects created without using the <see cref="ActivitySource"/> API.
        /// </remarks>
        /// <param name="operationName">Operation name of the <see cref="Activity"/> objects to capture.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder AddLegacySource(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException($"{nameof(operationName)} contains null or whitespace string.");
            }

            this.options.LegacyActivityOperationNames[operationName] = true;

            return this;
        }

        internal TracerProvider Build()
        {
            return new TracerProviderSdk(this.options);
        }
    }
}
