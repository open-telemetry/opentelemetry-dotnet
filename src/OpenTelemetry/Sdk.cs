// <copyright file="Sdk.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;
using static OpenTelemetry.Metrics.MeterProviderSdk;

namespace OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry helper.
    /// </summary>
    public static class Sdk
    {
        private static readonly TimeSpan DefaultPushInterval = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gets a value indicating whether instrumentation is suppressed (disabled).
        /// </summary>
        public static bool SuppressInstrumentation => SuppressInstrumentationScope.IsSuppressed;

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

            var metricProcessor = meterBuilder.MetricProcessor ?? new NoopMetricProcessor();
            var metricExporter = meterBuilder.MetricExporter ?? new NoopMetricExporter();
            var cancellationTokenSource = new CancellationTokenSource();
            var meterRegistry = new Dictionary<MeterRegistryKey, MeterSdk>();

            // We only have PushMetricController now with only configurable thing being the push interval
            var controller = new PushMetricController(
                meterRegistry,
                metricProcessor,
                metricExporter,
                meterBuilder.MetricPushInterval == default ? DefaultPushInterval : meterBuilder.MetricPushInterval,
                cancellationTokenSource);

            var meterProviderSdk = new MeterProviderSdk(metricProcessor, meterRegistry, controller, cancellationTokenSource);

            return meterProviderSdk;
        }

        /// <summary>
        /// Creates TracerProviderBuilder which should be used to build
        /// TracerProvider.
        /// </summary>
        /// <returns>TracerProviderBuilder instance, which should be used to build TracerProvider.</returns>
        public static TracerProviderBuilder CreateTracerProviderBuilder()
        {
            return new TracerProviderBuilder();
        }
    }
}
