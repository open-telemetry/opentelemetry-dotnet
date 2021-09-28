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
using OpenTelemetry.Resources;
using OpenTelemetry.Shared;

namespace OpenTelemetry.Metrics
{
    internal class MeterProviderBuilderSdk : MeterProviderBuilder
    {
        private readonly List<InstrumentationFactory> instrumentationFactories = new List<InstrumentationFactory>();
        private readonly List<string> meterSources = new List<string>();
        private ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault();

        internal MeterProviderBuilderSdk()
        {
        }

        internal List<MetricReader> MetricReaders { get; } = new List<MetricReader>();

        public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
        {
            Guard.IsNotNull(instrumentationFactory, nameof(instrumentationFactory));

            this.instrumentationFactories.Add(
                new InstrumentationFactory(
                    typeof(TInstrumentation).Name,
                    "semver:" + typeof(TInstrumentation).Assembly.GetName().Version,
                    instrumentationFactory));

            return this;
        }

        public override MeterProviderBuilder AddSource(params string[] names)
        {
            Guard.IsNotNull(names, nameof(names));

            foreach (var name in names)
            {

                // TODO: Review exception - $"{nameof(names)} contains null or whitespace string."
                // it also used an ArgumentException instead of null exception..
                Guard.IsNotNullOrWhitespace(name, nameof(name));

                this.meterSources.Add(name);
            }

            return this;
        }

        internal MeterProviderBuilderSdk AddMetricReader(MetricReader metricReader)
        {
            if (this.MetricReaders.Count >= 1)
            {
                // TODO: Review exception
                throw new InvalidOperationException("Only one Metricreader is allowed.");
            }

            this.MetricReaders.Add(metricReader);
            return this;
        }

        internal MeterProviderBuilderSdk SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            Guard.IsNotNull(resourceBuilder, nameof(resourceBuilder));

            this.resourceBuilder = resourceBuilder;
            return this;
        }

        internal MeterProvider Build()
        {
            return new MeterProviderSdk(
                this.resourceBuilder.Build(),
                this.meterSources,
                this.instrumentationFactories,
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
