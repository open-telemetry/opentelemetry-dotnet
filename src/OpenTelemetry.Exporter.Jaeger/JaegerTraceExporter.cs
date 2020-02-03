// <copyright file="JaegerTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class JaegerTraceExporter : SpanExporter, IDisposable
    {
        private readonly IJaegerUdpBatcher batcher;
        private readonly TimeSpan maxFlushInterval;
        private readonly System.Timers.Timer maxFlushIntervalTimer;

        /// <summary>
        /// Flushing from the timer and indirectly from normal calls to ExportAsync must be synchronized.
        /// </summary>
        private readonly AsyncSemaphore batcherLock = new AsyncSemaphore(1);

        private bool disposedValue = false;

        public JaegerTraceExporter(JaegerExporterOptions options)
            : this(options, new JaegerUdpBatcher(options))
        {
        }

        public JaegerTraceExporter(JaegerExporterOptions options, IJaegerUdpBatcher batcher)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.batcher = batcher ?? throw new ArgumentNullException(nameof(batcher));

            this.maxFlushInterval = options.MaxFlushInterval;
            this.maxFlushIntervalTimer = new System.Timers.Timer
            {
                AutoReset = false,
                Enabled = false,
                Interval = this.maxFlushInterval.TotalMilliseconds,
            };

            this.maxFlushIntervalTimer.Elapsed += async (sender, args) =>
            {
                await this.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            };
        }

        /// <inheritdoc/>
        public override async Task<ExportResult> ExportAsync(IEnumerable<SpanData> otelSpanList, CancellationToken cancellationToken)
        {
            var jaegerSpans = otelSpanList.Select(sdl => sdl.ToJaegerSpan());
            var spanCount = jaegerSpans.Count();
            var flushedSpanCount = 0;

            using (await this.batcherLock.EnterAsync().ConfigureAwait(false))
            {
                foreach (var s in jaegerSpans)
                {
                    // avoid cancelling here: this is no return point: if we reached this point
                    // and cancellation is requested, it's better if we try to finish sending spans rather than drop it
                    flushedSpanCount += await this.batcher.AppendAsync(s, CancellationToken.None).ConfigureAwait(false);
                }

                // ensure the flush timer is active if there are spans that were not flushed.
                if (flushedSpanCount < spanCount)
                {
                    this.maxFlushIntervalTimer.Enabled = true;
                }
            }

            // TODO jaeger status to ExportResult
            return ExportResult.Success;
        }

        /// <inheritdoc/>
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return this.FlushAsync(cancellationToken);
        }

        /// <inheritdoc/>
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
                    this.batcher.Dispose();
                }

                this.disposedValue = true;
            }
        }

        private async Task<int> FlushAsync(CancellationToken cancellationToken)
        {
            using (await this.batcherLock.EnterAsync().ConfigureAwait(false))
            {
                this.maxFlushIntervalTimer.Enabled = false;
                return await this.batcher.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
