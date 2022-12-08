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

#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
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
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="enabled">Enabled or not. Default value is <c>true</c>.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetErrorStatusOnException(this TracerProviderBuilder tracerProviderBuilder, bool enabled = true)
        {
            tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
            {
                if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
                {
                    tracerProviderBuilderSdk.SetErrorStatusOnException(enabled);
                }
            });

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Sets sampler.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="sampler">Sampler instance.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetSampler(this TracerProviderBuilder tracerProviderBuilder, Sampler sampler)
        {
            Guard.ThrowIfNull(sampler);

            tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
            {
                if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
                {
                    tracerProviderBuilderSdk.SetSampler(sampler);
                }
            });

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Sets the sampler on the provider.
        /// </summary>
        /// <remarks>
        /// Note: The type specified by <typeparamref name="T"/> will be
        /// registered as a singleton service into application services.
        /// </remarks>
        /// <typeparam name="T">Sampler type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetSampler<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : Sampler
        {
            tracerProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

            tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
            {
                if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
                {
                    tracerProviderBuilderSdk.SetSampler(sp.GetRequiredService<T>());
                }
            });

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// You should usually use <see cref="ConfigureResource(TracerProviderBuilder, Action{ResourceBuilder})"/> instead
        /// (call <see cref="ResourceBuilder.Clear"/> if desired).
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetResourceBuilder(this TracerProviderBuilder tracerProviderBuilder, ResourceBuilder resourceBuilder)
        {
            Guard.ThrowIfNull(resourceBuilder);

            tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
            {
                if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
                {
                    tracerProviderBuilderSdk.SetResourceBuilder(resourceBuilder);
                }
            });

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Modify the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from in-place.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="configure">An action which modifies the provided <see cref="ResourceBuilder"/> in-place.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder ConfigureResource(this TracerProviderBuilder tracerProviderBuilder, Action<ResourceBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
            {
                if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
                {
                    tracerProviderBuilderSdk.ConfigureResource(configure);
                }
            });

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Adds a processor to the provider.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="processor">Activity processor to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddProcessor(this TracerProviderBuilder tracerProviderBuilder, BaseProcessor<Activity> processor)
        {
            Guard.ThrowIfNull(processor);

            tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
            {
                if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
                {
                    tracerProviderBuilderSdk.AddProcessor(processor);
                }
            });

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Adds a processor to the provider which will be retrieved using dependency injection.
        /// </summary>
        /// <remarks>
        /// Note: The type specified by <typeparamref name="T"/> will be
        /// registered as a singleton service into application services.
        /// </remarks>
        /// <typeparam name="T">Processor type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddProcessor<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : BaseProcessor<Activity>
        {
            tracerProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

            tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
            {
                if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
                {
                    tracerProviderBuilderSdk.AddProcessor(sp.GetRequiredService<T>());
                }
            });

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Run the given actions to initialize the <see cref="TracerProvider"/>.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns><see cref="TracerProvider"/>.</returns>
        public static TracerProvider? Build(this TracerProviderBuilder tracerProviderBuilder)
        {
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                return tracerProviderBuilderBase.InvokeBuild();
            }

            return null;
        }
    }
}
