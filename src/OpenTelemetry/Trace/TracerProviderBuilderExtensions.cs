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

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
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
        /// Sets the sampler on the provider.
        /// </summary>
        /// <typeparam name="T">Sampler type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetSampler<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : Sampler
        {
            return ConfigureBuilder(
                tracerProviderBuilder,
                (sp, builder) => builder.SetSampler(sp.GetRequiredService<T>()));
        }

        /// <summary>
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// You should usually use <see cref="ConfigureResource(TracerProviderBuilder, Action{ResourceBuilder})"/> instead
        /// (call <see cref="ResourceBuilder.Clear"/> if desired).
        /// </summary>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder SetResourceBuilder(this TracerProviderBuilder tracerProviderBuilder, ResourceBuilder resourceBuilder)
        {
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.ResourceBuilder = resourceBuilder;
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Modify the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from in-place.
        /// </summary>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <param name="configure">An action which modifies the provided <see cref="ResourceBuilder"/> in-place.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder ConfigureResource(this TracerProviderBuilder tracerProviderBuilder, Action<ResourceBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            return tracerProviderBuilder.ConfigureBuilder((sp, b) =>
            {
                // Note: ConfigureResource is deferred until the build phase.
                // This allows them to play nice and apply on top of any
                // SetResourceBuilder calls.
                if (b is TracerProviderBuilderBase tracerProviderBuilderBase)
                {
                    Debug.Assert(tracerProviderBuilderBase.ResourceBuilder != null, "ResourceBuilder was null");

                    configure(tracerProviderBuilderBase.ResourceBuilder!);
                }
            });
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
        /// Adds a processor to the provider which will be retrieved using dependency injection.
        /// </summary>
        /// <typeparam name="T">Processor type.</typeparam>
        /// <param name="tracerProviderBuilder">TracerProviderBuilder instance.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddProcessor<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : BaseProcessor<Activity>
        {
            return ConfigureBuilder(
                tracerProviderBuilder,
                (sp, provider) => provider.AddProcessor(sp.GetRequiredService<T>()));
        }

        /// <summary>
        /// Adds instrumentation to the provider.
        /// </summary>
        /// <typeparam name="T">Instrumentation type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddInstrumentation<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : class
        {
            return ConfigureBuilder(
                tracerProviderBuilder,
                (sp, builder) => builder.AddInstrumentation(() => sp.GetRequiredService<T>()));
        }

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="IServiceCollection"/> where tracing services are configured.
        /// </summary>
        /// <remarks>
        /// Note: Tracing services are only available during the application
        /// configuration phase.
        /// </remarks>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder ConfigureServices(
            this TracerProviderBuilder tracerProviderBuilder,
            Action<IServiceCollection> configure)
        {
            Guard.ThrowIfNull(configure);

            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                var services = tracerProviderBuilderBase.Services;

                if (services == null)
                {
                    throw new NotSupportedException("Services cannot be configured outside of application configuration phase.");
                }

                configure(services);
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="TracerProviderBuilder"/> once the application <see
        /// cref="IServiceProvider"/> is available.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder ConfigureBuilder(
            this TracerProviderBuilder tracerProviderBuilder,
            Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            if (tracerProviderBuilder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                deferredTracerProviderBuilder.Configure(configure);
            }

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
                return tracerProviderBuilderBase.Build();
            }

            return null;
        }
    }
}
