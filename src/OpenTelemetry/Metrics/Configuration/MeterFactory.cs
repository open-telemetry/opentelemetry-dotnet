// <copyright file="MeterFactory.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using System.Collections.Generic;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Configuration
{
    public class MeterFactory : MeterFactoryBase
    {
        private readonly object lck = new object();
        private readonly Dictionary<MeterRegistryKey, Meter> meterRegistry = new Dictionary<MeterRegistryKey, Meter>();
        private readonly MetricProcessor metricProcessor;
        private Meter defaultMeter;

        private MeterFactory(MetricProcessor metricProcessor)
        {
            if (metricProcessor == null)
            {
                this.metricProcessor = new NoOpMetricProcessor();
            }
            else
            {
                this.metricProcessor = metricProcessor;
            }

            this.defaultMeter = new MeterSdk(string.Empty, this.metricProcessor);
        }

        public static MeterFactory Create(MetricProcessor metricProcessor)
        {
            return new MeterFactory(metricProcessor);
        }

        public override Meter GetMeter(string name, string version = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return this.defaultMeter;
            }

            lock (this.lck)
            {
                var key = new MeterRegistryKey(name, version);
                if (!this.meterRegistry.TryGetValue(key, out var meter))
                {
                    meter = this.defaultMeter = new MeterSdk(name, this.metricProcessor);

                    this.meterRegistry.Add(key, meter);
                }

                return meter;
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> CreateLibraryResourceLabels(string name, string version)
        {
            var labels = new Dictionary<string, string> { { "name", name } };
            if (!string.IsNullOrEmpty(version))
            {
                labels.Add("version", version);
            }

            return labels;
        }

        private readonly struct MeterRegistryKey
        {
            private readonly string name;
            private readonly string version;

            internal MeterRegistryKey(string name, string version)
            {
                this.name = name;
                this.version = version;
            }
        }
    }
}
