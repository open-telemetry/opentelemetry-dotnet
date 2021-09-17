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
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics
{
    public class PeriodicExportingMetricReader : BaseExportingMetricReader
    {
        internal const int DefaultExportIntervalMilliseconds = 60000;
        internal const int DefaultExportTimeoutMilliseconds = 30000;

        private readonly Task exportTask;
        private readonly CancellationTokenSource token;
        private bool disposed;

        public PeriodicExportingMetricReader(
            BaseExporter<Metric> exporter,
            int exportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
            int exportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
            : base(exporter)
        {
            if ((this.SupportedExportModes & ExportModes.Push) != ExportModes.Push)
            {
                throw new InvalidOperationException("The exporter does not support push mode.");
            }

            this.token = new CancellationTokenSource();

            // TODO: Use dedicated thread.
            this.exportTask = new Task(() =>
            {
                while (!this.token.IsCancellationRequested)
                {
                    Task.Delay(exportIntervalMilliseconds).Wait();
                    this.Collect();
                }
            });

            this.exportTask.Start();
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
                    this.token.Cancel();
                    this.exportTask.Wait();
                    this.token.Dispose();
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
