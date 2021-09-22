// <copyright file="MeterProviderBuilderSdk.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    internal class MeterProviderBuilderSdk : MeterProviderBuilder
    {
        private readonly List<InstrumentationFactory> instrumentationFactories = new List<InstrumentationFactory>();
        private readonly List<string> meterSources = new List<string>();
        private readonly List<Func<Instrument, MetricStreamConfig>> viewConfigs = new List<Func<Instrument, MetricStreamConfig>>();
        private ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault();

        internal MeterProviderBuilderSdk()
        {
        }

        internal List<MetricReader> MetricReaders { get; } = new List<MetricReader>();

        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            if (instrumentationFactory == null)
            {
                throw new ArgumentNullException(nameof(instrumentationFactory));
            }

            this.instrumentationFactories.Add(
                new InstrumentationFactory(
                    typeof(TInstrumentation).Name,
                    "semver:" + typeof(TInstrumentation).Assembly.GetName().Version,
                    instrumentationFactory));

            return this;
        }

        public override MeterProviderBuilder AddSource(params string[] names)
        {
            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException($"{nameof(names)} contains null or whitespace string.");
                }

                this.meterSources.Add(name);
            }

            return this;
        }

        internal MeterProviderBuilderSdk AddMetricReader(MetricReader metricReader)
        {
            if (this.MetricReaders.Count >= 1)
            {
                throw new InvalidOperationException("Only one Metricreader is allowed.");
            }

            this.MetricReaders.Add(metricReader);
            return this;
        }

        internal MeterProviderBuilderSdk AddViewCallback(Func<Instrument, MetricStreamConfig> callback)
        {
            this.viewConfigs.Add(callback);
            return this;
        }

        internal MeterProviderBuilderSdk AddView(string name = "", string meterName = "", string instrumentName = "", string[] tagKeys = null, Aggregation aggregation = Aggregation.Default, double[] histogramBounds = null)
        {
            Func<Instrument, MetricStreamConfig> callBack = (instrument) =>
            {
                if (string.IsNullOrWhiteSpace(meterName) && string.IsNullOrWhiteSpace(instrumentName))
                {
                    throw new ArgumentException("Atleast one instrument selection criteria should be provided.");
                }

                // TODO: Do regex instead of simple StartsWith.
                if (instrument.Meter.Name.StartsWith(meterName)
                     && instrument.Name.StartsWith(instrumentName))
                {
                    var metricStreamConfig = new MetricStreamConfig();
                    metricStreamConfig.Name = string.IsNullOrWhiteSpace(name) ? instrument.Name : name;
                    metricStreamConfig.Aggregation = aggregation;
                    metricStreamConfig.TagKeys = tagKeys;
                    metricStreamConfig.HistogramBounds = histogramBounds;
                    return metricStreamConfig;
                }
                else
                {
                    return null;
                }
            };

            this.viewConfigs.Add(callBack);
            return this;
        }

        internal MeterProviderBuilderSdk SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            this.resourceBuilder = resourceBuilder ?? throw new ArgumentNullException(nameof(resourceBuilder));
            return this;
        }

        internal MeterProvider Build()
        {
            return new MeterProviderSdk(
                this.resourceBuilder.Build(),
                this.meterSources,
                this.instrumentationFactories,
                this.viewConfigs,
                this.MetricReaders.ToArray());
        }

        // TODO: This is copied from TracerProviderBuilderSdk. Move to common location.
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
