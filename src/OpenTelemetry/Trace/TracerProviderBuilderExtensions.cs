// <copyright file="TracerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Sets sampler.
        /// </summary>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="sampler">Sampler instance.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetSampler(this TracerProviderBuilder tracerProviderBuilder, Sampler sampler)
        {
            if (tracerProviderBuilder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.SetSampler(sampler);
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Sets the <see cref="Resource"/> describing the app associated with all traces. Overwrites currently set resource.
        /// </summary>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> to be associate with all traces.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetResourceBuilder(this TracerProviderBuilder tracerProviderBuilder, ResourceBuilder resourceBuilder)
        {
            if (tracerProviderBuilder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.SetResourceBuilder(resourceBuilder);
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Adds processor to the provider.
        /// </summary>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="processor">Activity processor to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddProcessor(this TracerProviderBuilder tracerProviderBuilder, BaseProcessor<Activity> processor)
        {
            if (tracerProviderBuilder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.AddProcessor(processor);
            }

            return tracerProviderBuilder;
        }

        public static TracerProvider Build(this TracerProviderBuilder tracerProviderBuilder)
        {
            if (tracerProviderBuilder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                return tracerProviderBuilderSdk.Build();
            }

            return null;
        }

        /// <summary>
        /// Adds a DiagnosticSource based instrumentation.
        /// This is required for libraries which is already instrumented with
        /// DiagnosticSource and Activity, without using ActivitySource.
        /// </summary>
        /// <typeparam name="TInstrumentation">Type of instrumentation class.</typeparam>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal static TracerProviderBuilder AddDiagnosticSourceInstrumentation<TInstrumentation>(
            this TracerProviderBuilder tracerProviderBuilder,
            Func<ActivitySourceAdapter, TInstrumentation> instrumentationFactory)
            where TInstrumentation : class
        {
            if (instrumentationFactory == null)
            {
                throw new ArgumentNullException(nameof(instrumentationFactory));
            }

            if (tracerProviderBuilder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.AddDiagnosticSourceInstrumentation(instrumentationFactory);
            }

            return tracerProviderBuilder;
        }
    }
}
