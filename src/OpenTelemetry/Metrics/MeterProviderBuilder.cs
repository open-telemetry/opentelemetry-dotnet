// <copyright file="MeterProviderBuilder.cs" company="OpenTelemetry Authors">
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
using static OpenTelemetry.Metrics.MeterProviderSdk;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Build MeterProvider with Exporter, Processor and PushInterval.
    /// </summary>
    public class MeterProviderBuilder
    {
        private static readonly TimeSpan DefaultPushInterval = TimeSpan.FromSeconds(60);
        private readonly List<InstrumentationFactory> instrumentationFactories = new List<InstrumentationFactory>();
        private MetricProcessor metricProcessor;
        private MetricExporter metricExporter;
        private TimeSpan metricPushInterval;

        internal MeterProviderBuilder()
        {
            this.metricExporter = new NoopMetricExporter();
            this.metricProcessor = new NoopMetricProcessor();
            this.metricPushInterval = DefaultPushInterval;
        }

        /// <summary>
        /// Sets processor.
        /// </summary>
        /// <param name="processor">Processor instance.</param>
        /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public MeterProviderBuilder SetProcessor(MetricProcessor processor)
        {
            this.metricProcessor = processor ?? new NoopMetricProcessor();
            return this;
        }

        /// <summary>
        /// Sets exporter.
        /// </summary>
        /// <param name="exporter">Exporter instance.</param>
        /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public MeterProviderBuilder SetExporter(MetricExporter exporter)
        {
            this.metricExporter = exporter ?? new NoopMetricExporter();
            return this;
        }

        /// <summary>
        /// Sets push interval.
        /// </summary>
        /// <param name="pushInterval">Push interval.</param>
        /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public MeterProviderBuilder SetPushInterval(TimeSpan pushInterval)
        {
            this.metricPushInterval = pushInterval == default ? DefaultPushInterval : pushInterval;
            return this;
        }

        public MeterProviderBuilder AddInstrumentation<TInstrumentation>(
            Func<Meter, TInstrumentation> instrumentationFactory)
            where TInstrumentation : class
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

        public MeterProvider Build()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var meterRegistry = new Dictionary<MeterRegistryKey, MeterSdk>();

            var controller = new PushMetricController(
                meterRegistry,
                this.metricProcessor,
                this.metricExporter,
                this.metricPushInterval,
                cancellationTokenSource);

            return new MeterProviderSdk(this.metricProcessor, this.instrumentationFactories, meterRegistry, controller, cancellationTokenSource);
        }

        internal readonly struct InstrumentationFactory
        {
            public readonly string Name;
            public readonly string Version;
            public readonly Func<Meter, object> Factory;

            internal InstrumentationFactory(string name, string version, Func<Meter, object> factory)
            {
                this.Name = name;
                this.Version = version;
                this.Factory = factory;
            }
        }
    }
}
