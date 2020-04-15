// <copyright file="MeterBuilder.cs" company="OpenTelemetry Authors">
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

using System;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Configuration
{
    public class MeterBuilder
    {
        internal MeterBuilder()
        {
        }

        internal MetricProcessor MetricProcessor { get; private set; }

        internal MetricExporter MetricExporter { get; private set; }

        internal TimeSpan MetricPushInterval { get; private set; }

        /// <summary>
        /// Configures metric processor. (aka batcher).
        /// </summary>
        /// <param name="metricProcessor">MetricProcessor instance.</param>
        /// <returns>The meter builder instance for chaining.</returns>
        public MeterBuilder SetMetricProcessor(MetricProcessor metricProcessor)
        {
            this.MetricProcessor = metricProcessor;
            return this;
        }

        /// <summary>
        /// Configures Metric Exporter.
        /// </summary>
        /// <param name="metricExporter">MetricExporter instance.</param>
        /// <returns>The meter builder instance for chaining.</returns>
        public MeterBuilder SetMetricExporter(MetricExporter metricExporter)
        {
            this.MetricExporter = metricExporter;
            return this;
        }

        /// <summary>
        /// Sets the push interval.
        /// </summary>
        /// <param name="pushInterval">push interval.</param>
        /// <returns>The meter builder instance for chaining.</returns>
        public MeterBuilder SetMetricPushInterval(TimeSpan pushInterval)
        {
            this.MetricPushInterval = pushInterval;
            return this;
        }
    }
}
