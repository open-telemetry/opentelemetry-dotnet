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
        private readonly Task exportTask;
        private readonly CancellationTokenSource token;

        public PeriodicExportingMetricReader(BaseExporter<Metric> exporter, int exportIntervalMs)
            : base(exporter)
        {
            this.token = new CancellationTokenSource();

            // TODO: Use dedicated thread.
            this.exportTask = new Task(() =>
            {
                while (!this.token.IsCancellationRequested)
                {
                    Task.Delay(exportIntervalMs).Wait();
                    this.Collect();
                }
            });

            this.exportTask.Start();
        }

        public override void OnCollect(Batch<Metric> metrics)
        {
            this.exporter.Export(metrics);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.disposed)
            {
                try
                {
                    this.token.Cancel();
                    this.token.Dispose();
                    this.exportTask.Wait();
                }
                catch (Exception)
                {
                    // TODO: Log
                }
            }

            base.Dispose(disposing);
        }
    }
}
