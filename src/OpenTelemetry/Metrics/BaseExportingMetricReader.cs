// <copyright file="BaseExportingMetricReader.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    public class BaseExportingMetricReader : MetricReader
    {
        protected readonly BaseExporter<Metric> exporter;
        private readonly ExportModes supportedExportModes = ExportModes.Push | ExportModes.Pull;
        private bool disposed;

        public BaseExportingMetricReader(BaseExporter<Metric> exporter)
        {
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));

            var exportorType = exporter.GetType();
            var attributes = exportorType.GetCustomAttributes(typeof(AggregationTemporalityAttribute), true);
            if (attributes.Length > 0)
            {
                var attr = (AggregationTemporalityAttribute)attributes[attributes.Length - 1];
                this.PreferredAggregationTemporality = attr.Preferred;
                this.SupportedAggregationTemporality = attr.Supported;
            }

            attributes = exportorType.GetCustomAttributes(typeof(ExportModesAttribute), true);
            if (attributes.Length > 0)
            {
                var attr = (ExportModesAttribute)attributes[attributes.Length - 1];
                this.supportedExportModes = attr.Supported;
            }
        }

        protected ExportModes SupportedExportModes => this.supportedExportModes;

        public override void OnCollect(Batch<Metric> metrics)
        {
            this.exporter.Export(metrics);
        }

        internal override void SetParentProvider(BaseProvider parentProvider)
        {
            base.SetParentProvider(parentProvider);
            this.exporter.ParentProvider = parentProvider;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    this.exporter.Dispose();
                }
                catch (Exception)
                {
                    // TODO: Log
                }
            }

            this.disposed = true;

            base.Dispose(disposing);
        }
    }
}
