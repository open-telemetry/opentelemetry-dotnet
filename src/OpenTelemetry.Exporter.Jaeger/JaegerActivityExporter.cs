// <copyright file="JaegerActivityExporter.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class JaegerActivityExporter : ActivityExporter, IDisposable
    {
        private readonly SemaphoreSlim exportLock = new SemaphoreSlim(1);
        private bool disposedValue = false; // To detect redundant dispose calls

        public JaegerActivityExporter(JaegerExporterOptions options)
        {
            this.JaegerAgentUdpBatcher = new JaegerUdpBatcher(options);
        }

        internal IJaegerUdpBatcher JaegerAgentUdpBatcher { get; }

        public override async Task<ExportResult> ExportAsync(IEnumerable<Activity> activityBatch, CancellationToken cancellationToken)
        {
            await this.exportLock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var activity in activityBatch)
                {
                    // TODO: group by activity source

                    // avoid cancelling here: this is no return point: if we reached this point
                    // and cancellation is requested, it's better if we try to finish sending spans rather than drop it
                    await this.JaegerAgentUdpBatcher.AppendAsync(activity.ToJaegerSpan(), CancellationToken.None).ConfigureAwait(false);
                }

                // TODO jaeger status to ExportResult
                return ExportResult.Success;
            }
            finally
            {
                this.exportLock.Release();
            }
        }

        public override async Task ShutdownAsync(CancellationToken cancellationToken)
        {
            await this.JaegerAgentUdpBatcher.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing).
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.JaegerAgentUdpBatcher.Dispose();
                }

                this.disposedValue = true;
            }
        }
    }
}
