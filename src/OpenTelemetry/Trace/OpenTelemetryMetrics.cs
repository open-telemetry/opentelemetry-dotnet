// <copyright file="OpenTelemetryMetrics.cs" company="OpenTelemetry Authors">
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
    /// OpenTelemetry helper.
    /// </summary>
    public class OpenTelemetryMetrics
    {
        private static TimeSpan defaultPushInterval = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Creates MeterProvider with the configuration provided.
        /// Configuration involves MetricProcessor, Exporter and push internval.
        /// </summary>
        /// <param name="configure">Action to configure MeterBuilder.</param>
        /// <returns>MeterProvider instance, which must be disposed upon shutdown.</returns>
        public static MeterProvider CreateMeterProvider(Action<MeterBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var meterBuilder = new MeterBuilder();
            configure(meterBuilder);

            var metricProcessor = meterBuilder.MetricProcessor ?? new NoOpMetricProcessor();
            var metricExporter = meterBuilder.MetricExporter ?? new NoOpMetricExporter();
            var cancellationTokenSource = new CancellationTokenSource();
            var meterRegistry = new Dictionary<MeterRegistryKey, MeterSdk>();

            // We only have PushMetricController now with only configurable thing being the push interval
            var controller = new PushMetricController(
                meterRegistry,
                metricProcessor,
                metricExporter,
                meterBuilder.MetricPushInterval == default(TimeSpan) ? defaultPushInterval : meterBuilder.MetricPushInterval,
                cancellationTokenSource);

            var meterProviderSdk = new MeterProviderSdk(metricProcessor, meterRegistry, controller, cancellationTokenSource);

            return meterProviderSdk;
        }
    }
}
