// <copyright file="MeterProviderBuilderBase.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains methods for building <see cref="MeterProvider"/> instances.
    /// </summary>
    public abstract class MeterProviderBuilderBase : MeterProviderBuilder, IDeferredMeterProviderBuilder
    {
        internal readonly MeterProviderBuilderState? State;

        private readonly bool ownsServices;
        private IServiceCollection? services;

        // This ctor is for a builder created from MeterProviderBuilderState which
        // happens after the service provider has been created.
        internal MeterProviderBuilderBase(MeterProviderBuilderState state)
        {
            Debug.Assert(state != null, "state was null");

            this.State = state;
        }

        // This ctor is for ConfigureOpenTelemetryMetrics +
        // AddOpenTelemetryMetrics scenarios where the builder is bound to an
        // external service collection.
        internal MeterProviderBuilderBase(IServiceCollection services)
        {
            Guard.ThrowIfNull(services);

            services.AddOpenTelemetryMeterProviderBuilderServices();
            services.TryAddSingleton<MeterProvider>(sp => new MeterProviderSdk(sp, ownsServiceProvider: false));

            this.services = services;
            this.ownsServices = false;
        }

        // This ctor is for Sdk.CreateMeterProviderBuilder where the builder
        // owns its services and service provider.
        protected MeterProviderBuilderBase()
        {
            var services = new ServiceCollection();

            services.AddOpenTelemetryMeterProviderBuilderServices();
            services.AddSingleton<MeterProvider>(
                sp => throw new NotSupportedException("External MeterProvider created through Sdk.CreateMeterProviderBuilder cannot be accessed using service provider."));

            this.services = services;
            this.ownsServices = true;
        }

        /// <inheritdoc />
        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            Guard.ThrowIfNull(instrumentationFactory);

            return this.AddInstrumentation((sp) => instrumentationFactory());
        }

        /// <inheritdoc />
        public override MeterProviderBuilder AddMeter(params string[] names)
        {
            Guard.ThrowIfNull(names);

            return this.ConfigureState((sp, state) => state.AddMeter(names));
        }

        /// <inheritdoc />
        MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(
            Action<IServiceProvider, MeterProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            if (this.State != null)
            {
                configure(this.State.ServiceProvider, this);
            }
            else
            {
                this.ConfigureServices(services
                    => MeterProviderBuilderServiceCollectionHelper.RegisterConfigureBuilderCallback(services, configure));
            }

            return this;
        }

        internal MeterProviderBuilder AddInstrumentation<T>()
            where T : class
        {
            this.TryAddSingleton<T>();
            this.AddInstrumentation((sp) => sp.GetRequiredService<T>());

            return this;
        }

        internal MeterProviderBuilder AddReader<T>()
            where T : MetricReader
        {
            this.TryAddSingleton<T>();
            this.ConfigureState((sp, state) => state.AddReader(sp.GetRequiredService<T>()));

            return this;
        }

        internal MeterProviderBuilder AddReader(MetricReader reader)
        {
            Guard.ThrowIfNull(reader);

            return this.ConfigureState((sp, state) => state.AddReader(reader));
        }

        internal MeterProviderBuilder AddView(string instrumentName, string name)
        {
            if (!MeterProviderBuilderSdk.IsValidInstrumentName(name))
            {
                throw new ArgumentException($"Custom view name {name} is invalid.", nameof(name));
            }

            if (instrumentName.IndexOf('*') != -1)
            {
                throw new ArgumentException(
                    $"Instrument selection criteria is invalid. Instrument name '{instrumentName}' " +
                    $"contains a wildcard character. This is not allowed when using a view to " +
                    $"rename a metric stream as it would lead to conflicting metric stream names.",
                    nameof(instrumentName));
            }

            return this.AddView(
                instrumentName,
                new MetricStreamConfiguration
                {
                    Name = name,
                });
        }

        internal MeterProviderBuilder AddView(string instrumentName, MetricStreamConfiguration metricStreamConfiguration)
        {
            Guard.ThrowIfNullOrWhitespace(instrumentName);
            Guard.ThrowIfNull(metricStreamConfiguration);

            if (metricStreamConfiguration.Name != null && instrumentName.IndexOf('*') != -1)
            {
                throw new ArgumentException(
                    $"Instrument selection criteria is invalid. Instrument name '{instrumentName}' " +
                    $"contains a wildcard character. This is not allowed when using a view to " +
                    $"rename a metric stream as it would lead to conflicting metric stream names.",
                    nameof(instrumentName));
            }

            if (instrumentName.IndexOf('*') != -1)
            {
                var pattern = '^' + Regex.Escape(instrumentName).Replace("\\*", ".*");
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                return this.AddView(instrument => regex.IsMatch(instrument.Name) ? metricStreamConfiguration : null);
            }
            else
            {
                return this.AddView(instrument => instrument.Name.Equals(instrumentName, StringComparison.OrdinalIgnoreCase) ? metricStreamConfiguration : null);
            }
        }

        internal MeterProviderBuilder AddView(Func<Instrument, MetricStreamConfiguration?> viewConfig)
        {
            Guard.ThrowIfNull(viewConfig);

            this.ConfigureState((sp, state) => state.AddView(viewConfig));

            return this;
        }

        internal MeterProviderBuilder ConfigureResource(Action<ResourceBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            return this.ConfigureState((sp, state) => state.ConfigureResource(configure));
        }

        internal MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
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

        internal MeterProvider InvokeBuild()
            => this.Build();

        internal MeterProviderBuilder SetMaxMetricStreams(int maxMetricStreams)
        {
            Guard.ThrowIfOutOfRange(maxMetricStreams, min: 1);

            return this.ConfigureState((sp, state) => state.MaxMetricStreams = maxMetricStreams);
        }

        internal MeterProviderBuilder SetMaxMetricPointsPerMetricStream(int maxMetricPointsPerMetricStream)
        {
            Guard.ThrowIfOutOfRange(maxMetricPointsPerMetricStream, min: 1);

            return this.ConfigureState((sp, state) => state.MaxMetricPointsPerMetricStream = maxMetricPointsPerMetricStream);
        }

        internal MeterProviderBuilder SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            Guard.ThrowIfNull(resourceBuilder);

            return this.ConfigureState((sp, state) => state.SetResourceBuilder(resourceBuilder));
        }

        /// <summary>
        /// Run the configured actions to initialize the <see cref="MeterProvider"/>.
        /// </summary>
        /// <returns><see cref="MeterProvider"/>.</returns>
        protected MeterProvider Build()
        {
            if (!this.ownsServices || this.State != null)
            {
                throw new NotSupportedException("Build cannot be called directly on MeterProviderBuilder tied to external services.");
            }

            var services = this.services;

            if (services == null)
            {
                throw new NotSupportedException("MeterProviderBuilder build method cannot be called multiple times.");
            }

            this.services = null;

#if DEBUG
            bool validateScopes = true;
#else
            bool validateScopes = false;
#endif
            var serviceProvider = services.BuildServiceProvider(validateScopes);

            return new MeterProviderSdk(serviceProvider, ownsServiceProvider: true);
        }

        private MeterProviderBuilder AddInstrumentation<T>(Func<IServiceProvider, T> instrumentationFactory)
            where T : class
        {
            this.ConfigureState((sp, state)
                => state.AddInstrumentation(
                    typeof(T).Name,
                    "semver:" + typeof(T).Assembly.GetName().Version,
                    instrumentationFactory(sp)));

            return this;
        }

        private MeterProviderBuilder ConfigureState(Action<IServiceProvider, MeterProviderBuilderState> configure)
        {
            Debug.Assert(configure != null, "configure was null");

            if (this.State != null)
            {
                configure!(this.State.ServiceProvider, this.State);
            }
            else
            {
                this.ConfigureServices(services => MeterProviderBuilderServiceCollectionHelper.RegisterConfigureStateCallback(services, configure!));
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
