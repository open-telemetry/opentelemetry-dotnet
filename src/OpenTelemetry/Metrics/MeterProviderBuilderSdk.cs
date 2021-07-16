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

namespace OpenTelemetry.Metrics
{
    internal class MeterProviderBuilderSdk : MeterProviderBuilder
    {
        private readonly List<string> meterSources = new List<string>();

        internal MeterProviderBuilderSdk()
        {
        }

        internal List<MeasurementProcessor> MeasurementProcessors { get; } = new List<MeasurementProcessor>();

        internal List<MetricProcessor> MetricProcessors { get; } = new List<MetricProcessor>();

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

        internal MeterProviderBuilderSdk AddMeasurementProcessor(MeasurementProcessor processor)
        {
            this.MeasurementProcessors.Add(processor);
            return this;
        }

        internal MeterProviderBuilderSdk AddMetricProcessor(MetricProcessor processor)
        {
            this.MetricProcessors.Add(processor);
            return this;
        }

        internal MeterProvider Build()
        {
            return new MeterProviderSdk(
                this.meterSources,
                this.MeasurementProcessors.ToArray(),
                this.MetricProcessors.ToArray());
        }
    }
}
