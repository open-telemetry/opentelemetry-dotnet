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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Stores state used to build a <see cref="MeterProvider"/>.
    /// </summary>
    internal sealed class MeterProviderBuilderState : ProviderBuilderState<MeterProviderBuilderSdk, MeterProviderSdk>
    {
        public const int MaxMetricsDefault = 1000;
        public const int MaxMetricPointsPerMetricDefault = 2000;

        private MeterProviderBuilderSdk? builder;

        public MeterProviderBuilderState(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        public override MeterProviderBuilderSdk Builder
            => this.builder ??= new MeterProviderBuilderSdk(this);

        public List<MetricReader> Readers { get; } = new();

        public List<string> MeterSources { get; } = new();

        public List<Func<Instrument, MetricStreamConfiguration?>> ViewConfigs { get; } = new();

        public int MaxMetricStreams { get; set; } = MaxMetricsDefault;

        public int MaxMetricPointsPerMetricStream { get; set; } = MaxMetricPointsPerMetricDefault;

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
    }
}
