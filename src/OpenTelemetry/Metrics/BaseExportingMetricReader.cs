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
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public class BaseExportingMetricReader : MetricReader
    {
        protected readonly BaseExporter<Metric> exporter;
        private readonly ExportModes supportedExportModes = ExportModes.Push | ExportModes.Pull;
        private bool disposed;

        public BaseExportingMetricReader(BaseExporter<Metric> exporter)
        {
            Guard.Null(exporter, nameof(exporter));

            this.exporter = exporter;

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

            if (exporter is IPullMetricExporter pullExporter)
            {
                if (this.supportedExportModes.HasFlag(ExportModes.Push))
                {
                    pullExporter.Collect = this.Collect;
                }
                else
                {
                    pullExporter.Collect = (timeoutMilliseconds) =>
                    {
                        using (PullMetricScope.Begin())
                        {
                            return this.Collect(timeoutMilliseconds);
                        }
                    };
                }
            }
        }

        internal BaseExporter<Metric> Exporter => this.exporter;

        protected ExportModes SupportedExportModes => this.supportedExportModes;

        internal override void SetParentProvider(BaseProvider parentProvider)
        {
            base.SetParentProvider(parentProvider);
            this.exporter.ParentProvider = parentProvider;
        }

        /// <inheritdoc/>
        protected override bool ProcessMetrics(Batch<Metric> metrics, int timeoutMilliseconds)
        {
            // TODO: Do we need to consider timeout here?
            return this.exporter.Export(metrics) == ExportResult.Success;
        }

        /// <inheritdoc />
        protected override bool OnCollect(int timeoutMilliseconds)
        {
            if (this.supportedExportModes.HasFlag(ExportModes.Push))
            {
                return base.OnCollect(timeoutMilliseconds);
            }

            if (this.supportedExportModes.HasFlag(ExportModes.Pull) && PullMetricScope.IsPullAllowed)
            {
                return base.OnCollect(timeoutMilliseconds);
            }

            // TODO: add some error log
            return false;
        }

        /// <inheritdoc />
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            var result = true;

            if (timeoutMilliseconds == Timeout.Infinite)
            {
                result = this.Collect(Timeout.Infinite) && result;
                result = this.exporter.Shutdown(Timeout.Infinite) && result;
            }
            else
            {
                var sw = Stopwatch.StartNew();
                result = this.Collect(timeoutMilliseconds) && result;
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
                result = this.exporter.Shutdown((int)Math.Max(timeout, 0)) && result;
            }

            return result;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    try
                    {
                        if (this.exporter is IPullMetricExporter pullExporter)
                        {
                            pullExporter.Collect = null;
                        }

                        this.exporter.Dispose();
                    }
                    catch (Exception)
                    {
                        // TODO: Log
                    }
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
