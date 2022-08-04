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

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Build TracerProvider with Resource, Sampler, Processors and Instrumentation.
    /// </summary>
    public abstract class TracerProviderBuilderBase : TracerProviderBuilder, IDeferredTracerProviderBuilder
    {
        internal IServiceCollection? Services;
        internal List<Action<IServiceProvider, TracerProviderBuilder>>? BuilderConfigurationActions = new();
        internal ResourceBuilder? ResourceBuilder;

        private readonly List<InstrumentationFactory> instrumentationFactories = new();
        private readonly List<BaseProcessor<Activity>> processors = new();
        private readonly List<string> sources = new();
        private readonly HashSet<string> legacyActivityOperationNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly bool ownsServices;
        private Sampler? sampler;

        protected TracerProviderBuilderBase()
        {
            var services = new ServiceCollection();

            services.AddOptions();

            this.Services = services;
            this.ownsServices = true;
        }

        protected TracerProviderBuilderBase(IServiceCollection services)
        {
            Guard.ThrowIfNull(services);

            this.Services = services;
            this.ownsServices = false;
        }

        protected IServiceProvider? ServiceProvider { get; set; }

        /// <inheritdoc />
        public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(
            Func<TInstrumentation> instrumentationFactory)
            where TInstrumentation : class
        {
            Guard.ThrowIfNull(instrumentationFactory);

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
            Guard.ThrowIfNull(names);

            foreach (var name in names)
            {
                Guard.ThrowIfNullOrWhitespace(name);

                // TODO: We need to fix the listening model.
                // Today it ignores version.
                this.sources.Add(name);
            }

            return this;
        }

        /// <inheritdoc />
        public override TracerProviderBuilder AddLegacySource(string operationName)
        {
            Guard.ThrowIfNullOrWhitespace(operationName);

            this.legacyActivityOperationNames.Add(operationName);

            return this;
        }

        TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(
            Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            var configurationActions = this.BuilderConfigurationActions;
            if (configurationActions == null)
            {
                throw new NotSupportedException("Configuration actions cannot be registered after TracerProvider has been created.");
            }

            configurationActions.Add(configure);

            return this;
        }

        /// <summary>
        /// Sets whether the status of <see cref="Activity"/>
        /// should be set to <c>Status.Error</c> when it ended abnormally due to an unhandled exception.
        /// </summary>
        /// <param name="enabled">Enabled or not.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder SetErrorStatusOnException(bool enabled)
        {
            ExceptionProcessor? existingExceptionProcessor = null;

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
                        throw new NotSupportedException($"'{nameof(this.SetErrorStatusOnException)}' is not supported on this platform", ex);
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
            Guard.ThrowIfNull(sampler);

            this.sampler = sampler;

            return this;
        }

        /// <summary>
        /// Adds processor to the provider.
        /// </summary>
        /// <param name="processor">Activity processor to add.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        internal TracerProviderBuilder AddProcessor(BaseProcessor<Activity> processor)
        {
            Guard.ThrowIfNull(processor);

            this.processors.Add(processor);

            return this;
        }

        internal TracerProvider InvokeBuild()
        {
            return this.Build();
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
            return this.OnBuild();
        }

        /// <summary>
        /// Called to run the configured actions to initialize the <see cref="TracerProvider"/>.
        /// </summary>
        /// <returns><see cref="TracerProvider"/>.</returns>
        protected virtual TracerProvider OnBuild()
        {
            var services = this.Services;

            if (services == null)
            {
                throw new NotSupportedException("TracerProviderBuilder build method cannot be called multiple times.");
            }

            this.Services = null;

            var serviceProvider = this.ServiceProvider;
            var ownsServiceProvider = false;

            if (serviceProvider == null)
            {
                if (!this.ownsServices)
                {
                    throw new NotSupportedException("ServiceProvider was not supplied for builder tied to external services.");
                }

                serviceProvider = services.BuildServiceProvider();
                ownsServiceProvider = true;
            }
            else if (this.ownsServices)
            {
                throw new NotSupportedException("ServiceProvider was supplied for builder tied to internal services.");
            }

            this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

            // Step 1: Look for any Action<IServiceProvider,
            // TracerProviderBuilder> configuration actions registered and
            // execute them.

            var registeredConfigurations = serviceProvider.GetServices<Action<IServiceProvider, TracerProviderBuilder>>();
            foreach (var registeredConfiguration in registeredConfigurations)
            {
                registeredConfiguration?.Invoke(serviceProvider, this);
            }

            // Step 2: Execute any configuration actions directly attached to
            // the builder.

            var configurationActions = this.BuilderConfigurationActions;
            if (configurationActions != null)
            {
                // Note: Not using a foreach loop because additional actions can be
                // added during each call.
                for (int i = 0; i < configurationActions.Count; i++)
                {
                    configurationActions[i](serviceProvider, this);
                }

                this.BuilderConfigurationActions = null;
            }

            // Step 3: Look for any samplers registered.

            var registeredSampler = serviceProvider.GetService<Sampler>();
            var sampler = this.sampler;
            if (sampler == null)
            {
                sampler = registeredSampler ?? new ParentBasedSampler(new AlwaysOnSampler());
            }
            else if (registeredSampler != null)
            {
                throw new NotSupportedException("A sampler was registered in application services and set on tracer builder directly.");
            }

            // Step 4: Look for any processors registered.

            var registeredProcessors = serviceProvider.GetServices<BaseProcessor<Activity>>();
            foreach (var registeredProcessor in registeredProcessors)
            {
                this.processors.Add(registeredProcessor);
            }

            return new TracerProviderSdk(
                this.ResourceBuilder.Build(),
                this.sources,
                this.instrumentationFactories,
                sampler,
                this.processors,
                this.legacyActivityOperationNames,
                ownsServiceProvider ? (ServiceProvider)serviceProvider : null);
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
