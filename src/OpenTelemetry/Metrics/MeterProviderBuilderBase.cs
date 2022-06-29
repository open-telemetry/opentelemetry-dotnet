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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Build MeterProvider with Instrumentations, Meters,
    /// Resource, Readers, and Views.
    /// </summary>
    public abstract class MeterProviderBuilderBase : MeterProviderBuilder
    {
        internal const int MaxMetricsDefault = 1000;
        internal const int MaxMetricPointsPerMetricDefault = 2000;
        private readonly List<InstrumentationFactory> instrumentationFactories = new();
        private readonly List<string> meterSources = new();
        private readonly List<Func<Instrument, MetricStreamConfiguration>> viewConfigs = new();
        private ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault();
        private int maxMetricStreams = MaxMetricsDefault;
        private int maxMetricPointsPerMetricStream = MaxMetricPointsPerMetricDefault;

        protected MeterProviderBuilderBase()
        {
        }

        internal List<MetricReader> MetricReaders { get; } = new List<MetricReader>();

        internal ResourceBuilder ResourceBuilder
        {
            get => this.resourceBuilder;
            set
            {
                Debug.Assert(value != null, $"{nameof(this.ResourceBuilder)} must not be set to null");
                this.resourceBuilder = value;
            }
        }

        /// <inheritdoc />
        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
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
        public override MeterProviderBuilder AddMeter(params string[] names)
        {
            Guard.ThrowIfNull(names);

            foreach (var name in names)
            {
                Guard.ThrowIfNullOrWhitespace(name);

                this.meterSources.Add(name);
            }

            return this;
        }

        internal MeterProviderBuilder AddReader(MetricReader reader)
        {
            this.MetricReaders.Add(reader);
            return this;
        }

        internal MeterProviderBuilder AddView(string instrumentName, string name)
        {
            return this.AddView(
                instrumentName,
                new MetricStreamConfiguration
                {
                    Name = name,
                });
        }

        internal MeterProviderBuilder AddView(string instrumentName, MetricStreamConfiguration metricStreamConfiguration)
        {
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

        internal MeterProviderBuilder AddView(Func<Instrument, MetricStreamConfiguration> viewConfig)
        {
            this.viewConfigs.Add(viewConfig);
            return this;
        }

        internal MeterProviderBuilder SetMaxMetricStreams(int maxMetricStreams)
        {
            Guard.ThrowIfOutOfRange(maxMetricStreams, min: 1);

            this.maxMetricStreams = maxMetricStreams;
            return this;
        }

        internal MeterProviderBuilder SetMaxMetricPointsPerMetricStream(int maxMetricPointsPerMetricStream)
        {
            Guard.ThrowIfOutOfRange(maxMetricPointsPerMetricStream, min: 1);

            this.maxMetricPointsPerMetricStream = maxMetricPointsPerMetricStream;
            return this;
        }

        /// <summary>
        /// Run the configured actions to initialize the <see cref="MeterProvider"/>.
        /// </summary>
        /// <returns><see cref="MeterProvider"/>.</returns>
        protected MeterProvider Build()
        {
            return new MeterProviderSdk(
                this.resourceBuilder.Build(),
                this.meterSources,
                this.instrumentationFactories,
                this.viewConfigs,
                this.maxMetricStreams,
                this.maxMetricPointsPerMetricStream,
                this.MetricReaders.ToArray());
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
