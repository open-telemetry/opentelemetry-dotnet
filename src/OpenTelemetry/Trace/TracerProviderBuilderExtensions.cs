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
        /// Sets whether the status of <see cref="System.Diagnostics.Activity"/>
        /// should be set to <c>Status.Error</c> when it ended abnormally due to an unhandled exception.
        /// </summary>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="enabled">Enabled or not. Default value is <c>true</c>.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetErrorStatusOnException(this TracerProviderBuilder tracerProviderBuilder, bool enabled = true)
        {
            if (tracerProviderBuilder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.SetErrorStatusOnException(enabled);
            }

            return tracerProviderBuilder;
        }

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
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// </summary>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
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

        /// <summary>
        /// Adds a listener for <see cref="Activity"/> objects created with the given operation name to the <see cref="TracerProviderBuilder"/>.
        /// </summary>
        /// <remarks>
        /// This is provided to capture legacy <see cref="Activity"/> objects created without using the <see cref="ActivitySource"/> API.
        /// </remarks>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/> instance.</param>
        /// <param name="operationName">Operation name of the <see cref="Activity"/> objects to capture.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddLegacySource(this TracerProviderBuilder tracerProviderBuilder, string operationName)
        {
            if (tracerProviderBuilder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.AddLegacySource(operationName);
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
    }
}
