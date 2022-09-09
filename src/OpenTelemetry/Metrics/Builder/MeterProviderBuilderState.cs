// <copyright file="MeterProviderBuilderState.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Stores state used to build a <see cref="MeterProvider"/>.
    /// </summary>
    internal sealed class MeterProviderBuilderState
    {
        internal const int MaxMetricsDefault = 1000;
        internal const int MaxMetricPointsPerMetricDefault = 2000;
        internal readonly IServiceProvider ServiceProvider;
        internal readonly List<InstrumentationRegistration> Instrumentation = new();
        internal readonly List<MetricReader> Readers = new();
        internal readonly List<string> MeterSources = new();
        internal readonly List<Func<Instrument, MetricStreamConfiguration?>> ViewConfigs = new();
        internal ResourceBuilder? ResourceBuilder;
        internal int MaxMetricStreams = MaxMetricsDefault;
        internal int MaxMetricPointsPerMetricStream = MaxMetricPointsPerMetricDefault;

        private MeterProviderBuilderSdk? builder;

        public MeterProviderBuilderState(IServiceProvider serviceProvider)
        {
            Debug.Assert(serviceProvider != null, "serviceProvider was null");

            this.ServiceProvider = serviceProvider!;
        }

        public MeterProviderBuilderSdk Builder => this.builder ??= new MeterProviderBuilderSdk(this);

        public void AddInstrumentation(
            string instrumentationName,
            string instrumentationVersion,
            object instrumentation)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationName), "instrumentationName was null or whitespace");
            Debug.Assert(!string.IsNullOrWhiteSpace(instrumentationVersion), "instrumentationVersion was null or whitespace");
            Debug.Assert(instrumentation != null, "instrumentation was null");

            this.Instrumentation.Add(
                new InstrumentationRegistration(
                    instrumentationName,
                    instrumentationVersion,
                    instrumentation!));
        }

        public void AddMeter(params string[] names)
        {
            Debug.Assert(names != null, "names was null");

            foreach (var name in names!)
            {
                Guard.ThrowIfNullOrWhitespace(name);

                this.MeterSources.Add(name);
            }
        }

        public void AddReader(MetricReader reader)
        {
            Debug.Assert(reader != null, "reader was null");

            this.Readers.Add(reader!);
        }

        public void AddView(Func<Instrument, MetricStreamConfiguration?> viewConfig)
        {
            Debug.Assert(viewConfig != null, "viewConfig was null");

            this.ViewConfigs.Add(viewConfig!);
        }

        public void ConfigureResource(Action<ResourceBuilder> configure)
        {
            Debug.Assert(configure != null, "configure was null");

            var resourceBuilder = this.ResourceBuilder ??= ResourceBuilder.CreateDefault();

            configure!(resourceBuilder);
        }

        public void SetResourceBuilder(ResourceBuilder resourceBuilder)
        {
            Debug.Assert(resourceBuilder != null, "resourceBuilder was null");

            this.ResourceBuilder = resourceBuilder;
        }

        internal readonly struct InstrumentationRegistration
        {
            public readonly string Name;
            public readonly string Version;
            public readonly object Instance;

            internal InstrumentationRegistration(string name, string version, object instance)
            {
                this.Name = name;
                this.Version = version;
                this.Instance = instance;
            }
        }
    }
}
