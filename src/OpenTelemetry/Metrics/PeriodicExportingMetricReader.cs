// <copyright file="PeriodicExportingMetricReader.cs" company="OpenTelemetry Authors">
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
    /// <summary>
    /// MetricReader implementation which exports metrics to the configured
    /// MetricExporter upon <see cref="MetricReader.Collect(int)"/>.
    /// Automatically invokes collect at configured time intervals.
    /// </summary>
    public class PeriodicExportingMetricReader : BaseExportingMetricReader
    {
        internal const int DefaultExportIntervalMilliseconds = 60000;
        internal const int DefaultExportTimeoutMilliseconds = 30000;

        private readonly int exportIntervalMilliseconds;
        private readonly int exportTimeoutMilliseconds;
        private readonly Thread exporterThread;
        private readonly AutoResetEvent exportTrigger = new AutoResetEvent(false);
        private readonly ManualResetEvent shutdownTrigger = new ManualResetEvent(false);
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PeriodicExportingMetricReader"/> class.
        /// </summary>
        /// <param name="exporter">Exporter instance to export Metrics to.</param>
        /// <param name="exportIntervalMilliseconds">The interval in milliseconds between two consecutive exports. The default value is 60000.</param>
        /// <param name="exportTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
        public PeriodicExportingMetricReader(
            BaseExporter<Metric> exporter,
            int exportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
            int exportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
            : base(exporter)
        {
            Guard.ThrowIfOutOfRange(exportIntervalMilliseconds, min: 1);
            Guard.ThrowIfOutOfRange(exportTimeoutMilliseconds, min: 0);

            if ((this.SupportedExportModes & ExportModes.Push) != ExportModes.Push)
            {
                throw new InvalidOperationException($"The '{nameof(exporter)}' does not support '{nameof(ExportModes)}.{nameof(ExportModes.Push)}'");
            }

            this.exportIntervalMilliseconds = exportIntervalMilliseconds;
            this.exportTimeoutMilliseconds = exportTimeoutMilliseconds;

            this.exporterThread = new Thread(new ThreadStart(this.ExporterProc))
            {
                IsBackground = true,
                Name = $"OpenTelemetry-{nameof(PeriodicExportingMetricReader)}-{exporter.GetType().Name}",
            };
            this.exporterThread.Start();
        }

        /// <inheritdoc />
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            var result = true;

            this.shutdownTrigger.Set();

            if (timeoutMilliseconds == Timeout.Infinite)
            {
                this.exporterThread.Join();
                result = this.exporter.Shutdown() && result;
            }
            else
            {
                var sw = Stopwatch.StartNew();
                result = this.exporterThread.Join(timeoutMilliseconds) && result;
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
                    this.exportTrigger.Dispose();
                    this.shutdownTrigger.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private void ExporterProc()
        {
            var sw = Stopwatch.StartNew();
            var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };

            while (true)
            {
                var timeout = (int)(this.exportIntervalMilliseconds - (sw.ElapsedMilliseconds % this.exportIntervalMilliseconds));

                int index;

                try
                {
                    index = WaitHandle.WaitAny(triggers, timeout);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                switch (index)
                {
                    case 0: // export
                        this.Collect(this.exportTimeoutMilliseconds);
                        break;
                    case 1: // shutdown
                        this.Collect(this.exportTimeoutMilliseconds); // TODO: do we want to use the shutdown timeout here?
                        return;
                    case WaitHandle.WaitTimeout: // timer
                        this.Collect(this.exportTimeoutMilliseconds);
                        break;
                }
            }
        }
    }
}
