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
        protected bool disposed;

        private readonly ExportMode supportedExportMode = ExportMode.Push | ExportMode.Pull;

        public BaseExportingMetricReader(BaseExporter<Metric> exporter)
        {
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));

            var exportType = exporter.GetType();
            var attributes = exportType.GetCustomAttributes(typeof(AggregationTemporalityAttribute), true);
            if (attributes.Length > 0)
            {
                var attr = (AggregationTemporalityAttribute)attributes[attributes.Length - 1];
                this.PreferredAggregationTemporality = attr.Preferred;
                this.SupportedAggregationTemporality = attr.Supported;
            }

            attributes = exportType.GetCustomAttributes(typeof(ExportModeAttribute), true);
            if (attributes.Length > 0)
            {
                var attr = (ExportModeAttribute)attributes[attributes.Length - 1];
                this.supportedExportMode = attr.Supported;
            }
        }

        protected ExportMode SupportedExportMode => this.supportedExportMode;

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
            base.Dispose(disposing);

            if (disposing && !this.disposed)
            {
                try
                {
                    this.exporter.Dispose();
                }
                catch (Exception)
                {
                    // TODO: Log
                }

                this.disposed = true;
            }
        }
    }
}
