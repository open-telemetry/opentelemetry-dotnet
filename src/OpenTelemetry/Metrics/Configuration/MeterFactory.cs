// <copyright file="MeterFactory.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Configuration
{
    public class MeterFactory : MeterFactoryBase
    {
        // TODO: make MeterFactory IDisposable to call Dispose on Exporter/Controller.
        private readonly object lck = new object();
        private readonly Dictionary<MeterRegistryKey, MeterSdk> meterRegistry = new Dictionary<MeterRegistryKey, MeterSdk>();
        private readonly MetricProcessor metricProcessor;
        private readonly MetricExporter metricExporter;
        private readonly TimeSpan defaultPushInterval = TimeSpan.FromSeconds(60);
        private MeterSdk defaultMeter;

        private MeterFactory(MeterBuilder meterBuilder)
        {
            this.metricProcessor = meterBuilder.MetricProcessor ?? new NoOpMetricProcessor();
            this.metricExporter = meterBuilder.MetricExporter ?? new NoOpMetricExporter();

            // We only have PushMetricController now with only configurable thing being the push interval
            this.PushMetricController = new PushMetricController(
                this.meterRegistry,
                this.metricProcessor,
                this.metricExporter,
                meterBuilder.MetricPushInterval == default(TimeSpan) ? this.defaultPushInterval : meterBuilder.MetricPushInterval,
                new CancellationTokenSource());

            this.defaultMeter = new MeterSdk(string.Empty, this.metricProcessor);
            this.meterRegistry.Add(new MeterRegistryKey(string.Empty, null), this.defaultMeter);
        }

        internal PushMetricController PushMetricController { get; }

        public static MeterFactory Create(Action<MeterBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var builder = new MeterBuilder();
            configure(builder);

            return new MeterFactory(builder);
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
