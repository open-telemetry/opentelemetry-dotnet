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
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
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
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.SetSampler<T>();
            }

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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.SetResourceBuilder(resourceBuilder);
            }

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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.ConfigureResource(configure);
            }

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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.AddProcessor(processor);
            }

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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.AddProcessor<T>();
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Adds an exporter to the provider.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="exporter">Activity exporter to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddExporter(
            this TracerProviderBuilder tracerProviderBuilder,
            ExportProcessorType exportProcessorType,
            BaseExporter<Activity> exporter)
            => AddExporter(tracerProviderBuilder, exportProcessorType, exporter, name: null, configure: null);

        /// <summary>
        /// Adds an exporter to the provider.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="exporter">Activity exporter to add.</param>
        /// <param name="configure">Callback action to configure <see
        /// cref="ExportActivityProcessorOptions"/>.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddExporter(
            this TracerProviderBuilder tracerProviderBuilder,
            ExportProcessorType exportProcessorType,
            BaseExporter<Activity> exporter,
            Action<ExportActivityProcessorOptions> configure)
        {
            Guard.ThrowIfNull(configure);

            return AddExporter(tracerProviderBuilder, exportProcessorType, exporter, name: null, configure);
        }

        /// <summary>
        /// Adds an exporter to the provider.
        /// </summary>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="exporter">Activity exporter to add.</param>
        /// <param name="name">Optional name which is used when retrieving options.</param>
        /// <param name="configure">Optional callback action to configure <see
        /// cref="ExportActivityProcessorOptions"/>.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddExporter(
            this TracerProviderBuilder tracerProviderBuilder,
            ExportProcessorType exportProcessorType,
            BaseExporter<Activity> exporter,
            string? name,
            Action<ExportActivityProcessorOptions>? configure)
        {
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.AddExporter(exportProcessorType, exporter, name, configure);
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Adds an exporter to the provider which will be retrieved using dependency injection.
        /// </summary>
        /// <remarks><inheritdoc cref="AddExporter{T}(TracerProviderBuilder, ExportProcessorType, string?, Action{ExportActivityProcessorOptions}?)" path="/remarks"/></remarks>
        /// <typeparam name="T">Exporter type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddExporter<T>(
            this TracerProviderBuilder tracerProviderBuilder,
            ExportProcessorType exportProcessorType)
            where T : BaseExporter<Activity>
            => AddExporter<T>(tracerProviderBuilder, exportProcessorType, name: null, configure: null);

        /// <summary>
        /// Adds an exporter to the provider which will be retrieved using dependency injection.
        /// </summary>
        /// <remarks><inheritdoc cref="AddExporter{T}(TracerProviderBuilder, ExportProcessorType, string?, Action{ExportActivityProcessorOptions}?)" path="/remarks"/></remarks>
        /// <typeparam name="T">Exporter type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="configure">Callback action to configure <see
        /// cref="ExportActivityProcessorOptions"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddExporter<T>(
            this TracerProviderBuilder tracerProviderBuilder,
            ExportProcessorType exportProcessorType,
            Action<ExportActivityProcessorOptions> configure)
            where T : BaseExporter<Activity>
        {
            Guard.ThrowIfNull(configure);

            return AddExporter<T>(tracerProviderBuilder, exportProcessorType, name: null, configure);
        }

        /// <summary>
        /// Adds an exporter to the provider which will be retrieved using dependency injection.
        /// </summary>
        /// <remarks>
        /// Note: The type specified by <typeparamref name="T"/> will be
        /// registered as a singleton service into application services.
        /// </remarks>
        /// <typeparam name="T">Exporter type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <param name="exportProcessorType"><see cref="ExportProcessorType"/>.</param>
        /// <param name="name">Optional name which is used when retrieving options.</param>
        /// <param name="configure">Optional callback action to configure <see
        /// cref="ExportActivityProcessorOptions"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddExporter<T>(
            this TracerProviderBuilder tracerProviderBuilder,
            ExportProcessorType exportProcessorType,
            string? name,
            Action<ExportActivityProcessorOptions>? configure)
            where T : BaseExporter<Activity>
        {
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.AddExporter<T>(exportProcessorType, name, configure);
            }

            return tracerProviderBuilder;
        }

        /// <summary>
        /// Adds instrumentation to the provider.
        /// </summary>
        /// <remarks>
        /// Note: The type specified by <typeparamref name="T"/> will be
        /// registered as a singleton service into application services.
        /// </remarks>
        /// <typeparam name="T">Instrumentation type.</typeparam>
        /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
        public static TracerProviderBuilder AddInstrumentation<T>(this TracerProviderBuilder tracerProviderBuilder)
            where T : class
        {
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.AddInstrumentation<T>();
            }

            return tracerProviderBuilder;
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
            if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
            {
                tracerProviderBuilderBase.ConfigureServices(configure);
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
                return tracerProviderBuilderBase.InvokeBuild();
            }

            return null;
        }
    }
}
