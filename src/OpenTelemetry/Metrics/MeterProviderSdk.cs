// <copyright file="MeterProviderSdk.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Threading;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics
{
    internal class MeterProviderSdk : MeterProvider
    {
        public MetricProcessor MetricProcessor;
        public PushMetricController PushMetricController;
        public CancellationTokenSource CancellationTokenSource;
        public Dictionary<MeterRegistryKey, MeterSdk> MeterRegistry;

        private readonly object syncObject = new object();
        private MeterSdk defaultMeter;

        internal MeterProviderSdk(MetricProcessor metricProcessor, Dictionary<MeterRegistryKey, MeterSdk> registry, PushMetricController controller, CancellationTokenSource cts)
        {
            this.MetricProcessor = metricProcessor;
            this.PushMetricController = controller;
            this.CancellationTokenSource = cts;
            this.defaultMeter = new MeterSdk(string.Empty, this.MetricProcessor);
            this.MeterRegistry = registry;
            this.MeterRegistry.Add(new MeterRegistryKey(string.Empty, null), this.defaultMeter);
        }

        public override Meter GetMeter(string name, string version = null)
        {
            if (string.IsNullOrEmpty(name))
            {
                return this.defaultMeter;
            }

            lock (this.syncObject)
            {
                var key = new MeterRegistryKey(name, version);
                if (!this.MeterRegistry.TryGetValue(key, out var meter))
                {
                    meter = this.defaultMeter = new MeterSdk(name, this.MetricProcessor);

                    this.MeterRegistry.Add(key, meter);
                }

                return meter;
            }
        }

        protected override void Dispose(bool disposing)
        {
            this.CancellationTokenSource.Dispose();

            // TODO: Actually flush the metric processor/exporer/controllers.

            base.Dispose(disposing);
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

        internal readonly struct MeterRegistryKey
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
