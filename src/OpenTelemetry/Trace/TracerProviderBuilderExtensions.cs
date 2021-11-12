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
    /// <summary>
    /// Contains extension methods for the <see cref="TracerProviderBuilder"/> class.
    /// </summary>
    public static class TracerProviderBuilderExtensions
    {
        /// <summary>
        /// Sets whether the status of <see cref="Activity"/>
        /// should be set to <c>Status.Error</c> when it ended abnormally due to an unhandled exception.
        /// </summary>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="enabled">Enabled or not. Default value is <c>true</c>.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetErrorStatusOnException(this TracerProviderBuilder tracerProviderBuilder, bool enabled = true)
        {
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.SetErrorStatusOnException(enabled);
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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.SetSampler(sampler);
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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.SetResourceBuilder(resourceBuilder);
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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.AddProcessor(processor);
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Run the given actions to initialize the <see cref="TracerProvider"/>.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns><see cref="TracerProvider"/>.</returns>
        public static TracerProvider Build(this TracerProviderBuilder tracerProviderBuilder)
        {
            if (tracerProviderBuilder is IDeferredTracerProviderBuilder)
            {
                throw new NotSupportedException($"'{nameof(TracerProviderBuilder)}' requires a '{nameof(IServiceProvider)}' to build");
            }

            if (tracerProviderBuilder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                return tracerProviderBuilderSdk.BuildSdk();
            }

            return null;
        }
    }
}
