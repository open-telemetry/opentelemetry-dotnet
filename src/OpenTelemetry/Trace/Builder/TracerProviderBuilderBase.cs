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
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

using CallbackHelper = OpenTelemetry.ProviderBuilderServiceCollectionCallbackHelper<
    OpenTelemetry.Trace.TracerProviderBuilderSdk,
    OpenTelemetry.Trace.TracerProviderSdk,
    OpenTelemetry.Trace.TracerProviderBuilderState>;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Contains methods for building <see cref="TracerProvider"/> instances.
    /// </summary>
    public abstract class TracerProviderBuilderBase : TracerProviderBuilder, IDeferredTracerProviderBuilder
    {
        internal readonly TracerProviderBuilderState? State;

        private readonly bool ownsServices;
        private IServiceCollection? services;

        // This ctor is for a builder created from TracerProviderBuilderState which
        // happens after the service provider has been created.
        internal TracerProviderBuilderBase(TracerProviderBuilderState state)
        {
            Debug.Assert(state != null, "state was null");

            this.State = state;
        }

        // This ctor is for ConfigureOpenTelemetryTracing +
        // AddOpenTelemetryTracing scenarios where the builder is bound to an
        // external service collection.
        internal TracerProviderBuilderBase(IServiceCollection services)
        {
            Guard.ThrowIfNull(services);

            services.AddOpenTelemetryTracerProviderBuilderServices();
            services.TryAddSingleton<TracerProvider>(sp => new TracerProviderSdk(sp, ownsServiceProvider: false));

            this.services = services;
            this.ownsServices = false;
        }

        // This ctor is for Sdk.CreateTracerProviderBuilder where the builder
        // owns its services and service provider.
        protected TracerProviderBuilderBase()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetryTracerProviderBuilderServices();
            services.AddSingleton<TracerProvider>(
                sp => throw new NotSupportedException("External TracerProvider created through Sdk.CreateTracerProviderBuilder cannot be accessed using service provider."));

            this.services = services;
            this.ownsServices = true;
        }

        /// <inheritdoc />
        public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(
            Func<TInstrumentation> instrumentationFactory)
            where TInstrumentation : class
        {
            Guard.ThrowIfNull(instrumentationFactory);

            return this.AddInstrumentation((sp) => instrumentationFactory());
        }

        /// <inheritdoc />
        public override TracerProviderBuilder AddSource(params string[] names)
        {
            Guard.ThrowIfNull(names);

            return this.ConfigureState((sp, state) => state.AddSource(names));
        }

        /// <inheritdoc />
        public override TracerProviderBuilder AddLegacySource(string operationName)
        {
            Guard.ThrowIfNullOrWhitespace(operationName);

            return this.ConfigureState((sp, state) => state.AddLegacySource(operationName));
        }

        /// <inheritdoc />
        TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(
            Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            if (this.State != null)
            {
                configure(this.State.ServiceProvider, this);
            }
            else
            {
                this.ConfigureServices(services
                    => CallbackHelper.RegisterConfigureBuilderCallback(services, configure));
            }

            return this;
        }

        internal TracerProviderBuilder AddExporter<T>(ExportProcessorType exportProcessorType, string? name, Action<ExportActivityProcessorOptions>? configure)
            where T : BaseExporter<Activity>
        {
            this.TryAddSingleton<T>();
            this.ConfigureState((sp, state)
                => state.AddProcessor(
                    BuildExportProcessor(state.ServiceProvider, exportProcessorType, sp.GetRequiredService<T>(), name, configure)));

            return this;
        }

        internal TracerProviderBuilder AddExporter(ExportProcessorType exportProcessorType, BaseExporter<Activity> exporter, string? name, Action<ExportActivityProcessorOptions>? configure)
        {
            Guard.ThrowIfNull(exporter);

            this.ConfigureState((sp, state)
                => state.AddProcessor(
                    BuildExportProcessor(state.ServiceProvider, exportProcessorType, exporter, name, configure)));

            return this;
        }

        internal TracerProviderBuilder AddInstrumentation<T>()
            where T : class
        {
            this.TryAddSingleton<T>();
            this.AddInstrumentation((sp) => sp.GetRequiredService<T>());

            return this;
        }

        internal TracerProviderBuilder AddProcessor<T>()
            where T : BaseProcessor<Activity>
        {
            this.TryAddSingleton<T>();
            this.ConfigureState((sp, state) => state.AddProcessor(sp.GetRequiredService<T>()));

            return this;
        }

        internal TracerProviderBuilder AddProcessor(BaseProcessor<Activity> processor)
        {
            Guard.ThrowIfNull(processor);

            return this.ConfigureState((sp, state) => state.AddProcessor(processor));
        }

        internal TracerProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            return this.ConfigureState((sp, state) => state.ConfigureResource(configure));
        }

        internal TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
        {
            Guard.ThrowIfNull(configure);

            var services = this.services;

            if (services == null)
            {
                throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");
            }

            configure(services);

            return this;
        }

        internal TracerProvider InvokeBuild()
            => this.Build();

        internal TracerProviderBuilder SetErrorStatusOnException(bool enabled)
        {
            return this.ConfigureState((sp, state) => state.SetErrorStatusOnException = enabled);
        }

        internal TracerProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            Guard.ThrowIfNull(resourceBuilder);

            return this.ConfigureState((sp, state) => state.SetResourceBuilder(resourceBuilder));
        }

        internal TracerProviderBuilder SetSampler<T>()
            where T : Sampler
        {
            this.TryAddSingleton<T>();
            this.ConfigureState((sp, state) => state.SetSampler(sp.GetRequiredService<T>()));

            return this;
        }

        internal TracerProviderBuilder SetSampler(Sampler sampler)
        {
            Guard.ThrowIfNull(sampler);

            return this.ConfigureState((sp, state) => state.SetSampler(sampler));
        }

        /// <summary>
        /// Adds instrumentation to the provider.
        /// </summary>d
        /// <param name="instrumentationName">Instrumentation name.</param>
        /// <param name="instrumentationVersion">Instrumentation version.</param>
        /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
        /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
        protected TracerProviderBuilder AddInstrumentation(
            string instrumentationName,
            string instrumentationVersion,
            Func<object> instrumentationFactory)
        {
            Guard.ThrowIfNullOrWhitespace(instrumentationName);
            Guard.ThrowIfNullOrWhitespace(instrumentationVersion);
            Guard.ThrowIfNull(instrumentationFactory);

            return this.ConfigureState((sp, state)
                => state.AddInstrumentation(
                    instrumentationName,
                    instrumentationVersion,
                    instrumentationFactory()));
        }

        /// <summary>
        /// Run the configured actions to initialize the <see cref="TracerProvider"/>.
        /// </summary>
        /// <returns><see cref="TracerProvider"/>.</returns>
        protected TracerProvider Build()
        {
            if (!this.ownsServices || this.State != null)
            {
                throw new NotSupportedException("Build cannot be called directly on TracerProviderBuilder tied to external services.");
            }

            var services = this.services;

            if (services == null)
            {
                throw new NotSupportedException("TracerProviderBuilder build method cannot be called multiple times.");
            }

            this.services = null;

#if DEBUG
            bool validateScopes = true;
#else
            bool validateScopes = false;
#endif
            var serviceProvider = services.BuildServiceProvider(validateScopes);

            return new TracerProviderSdk(serviceProvider, ownsServiceProvider: true);
        }

        private static BaseProcessor<Activity> BuildExportProcessor(
            IServiceProvider serviceProvider,
            ExportProcessorType exportProcessorType,
            BaseExporter<Activity> exporter,
            string? name,
            Action<ExportActivityProcessorOptions>? configure)
        {
            name ??= Options.DefaultName;

            switch (exportProcessorType)
            {
                case ExportProcessorType.Simple:
                    return new SimpleActivityExportProcessor(exporter);
                case ExportProcessorType.Batch:
                    var options = serviceProvider.GetRequiredService<IOptionsMonitor<ExportActivityProcessorOptions>>().Get(name);

                    options.ExportProcessorType = ExportProcessorType.Batch;

                    configure?.Invoke(options);

                    var batchOptions = options.BatchExportProcessorOptions;

                    return new BatchActivityExportProcessor(
                        exporter,
                        batchOptions.MaxQueueSize,
                        batchOptions.ScheduledDelayMilliseconds,
                        batchOptions.ExporterTimeoutMilliseconds,
                        batchOptions.MaxExportBatchSize);
                default:
                    throw new NotSupportedException($"ExportProcessorType '{exportProcessorType}' is not supported.");
            }
        }

        private TracerProviderBuilder AddInstrumentation<T>(Func<IServiceProvider, T> instrumentationFactory)
            where T : class
        {
            this.ConfigureState((sp, state)
                => state.AddInstrumentation(
                    typeof(T).Name,
                    "semver:" + typeof(T).Assembly.GetName().Version,
                    instrumentationFactory(sp)));

            return this;
        }

        private TracerProviderBuilder ConfigureState(Action<IServiceProvider, TracerProviderBuilderState> configure)
        {
            Debug.Assert(configure != null, "configure was null");

            if (this.State != null)
            {
                configure!(this.State.ServiceProvider, this.State);
            }
            else
            {
                this.ConfigureServices(services =>
                    CallbackHelper.RegisterConfigureStateCallback(services, configure!));
            }

            return this;
        }

        private void TryAddSingleton<T>()
            where T : class
        {
            var services = this.services;

            services?.TryAddSingleton<T>();
        }
    }
}
