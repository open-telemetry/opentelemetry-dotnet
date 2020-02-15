// <copyright file="JaegerUdpBatcher.cs" company="OpenTelemetry Authors">
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
using Thrift.Protocols;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public class JaegerUdpBatcher : IJaegerUdpBatcher
    {
        private readonly int maxPacketSize;
        private readonly ITProtocolFactory protocolFactory;
        private readonly JaegerThriftClientTransport clientTransport;
        private readonly JaegerThriftClient thriftClient;

        private readonly SemaphoreSlim flushLock = new SemaphoreSlim(1);
        private readonly TimeSpan maxFlushInterval;
        private readonly System.Timers.Timer maxFlushIntervalTimer;

        private Dictionary<string, Process> processCache;
        private int batchByteSize;

        private bool disposedValue = false; // To detect redundant calls

        public JaegerUdpBatcher(JaegerExporterOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.MaxFlushInterval <= TimeSpan.Zero)
            {
                options.MaxFlushInterval = TimeSpan.FromSeconds(10);
            }

            this.maxPacketSize = (!options.MaxPacketSize.HasValue || options.MaxPacketSize == 0) ? JaegerExporterOptions.DefaultMaxPacketSize : options.MaxPacketSize.Value;
            this.protocolFactory = new TCompactProtocol.Factory();
            this.clientTransport = new JaegerThriftClientTransport(options.AgentHost, options.AgentPort);
            this.thriftClient = new JaegerThriftClient(this.protocolFactory.GetProtocol(this.clientTransport));
            this.Process = new Process(options.ServiceName, options.ProcessTags);

            this.maxFlushInterval = options.MaxFlushInterval;
            this.maxFlushIntervalTimer = new System.Timers.Timer
            {
                AutoReset = false,
                Enabled = false,
                Interval = this.maxFlushInterval.TotalMilliseconds,
            };

            this.maxFlushIntervalTimer.Elapsed += async (sender, args) =>
            {
                await this.FlushAsyncInternal(false, CancellationToken.None).ConfigureAwait(false);
            };
        }

        public Process Process { get; private set; }

        internal IDictionary<string, Batch> CurrentBatches { get; } = new Dictionary<string, Batch>();

        public async Task<int> AppendAsync(JaegerSpan span, CancellationToken cancellationToken)
        {
            if (this.processCache == null)
            {
                this.Process.ByteSize = await this.GetSize(this.Process).ConfigureAwait(false);
                this.processCache = new Dictionary<string, Process>
                {
                    [this.Process.ServiceName] = this.Process,
                };
            }

            string spanServiceName = span.JaegerTags?.FirstOrDefault(t => t.Key == "peer.service")?.VStr ?? this.Process.ServiceName;

            if (!this.processCache.TryGetValue(spanServiceName, out var spanProcess))
            {
                spanProcess = new Process(spanServiceName, this.Process.Tags);
                spanProcess.ByteSize = await this.GetSize(spanProcess).ConfigureAwait(false);
                this.processCache.Add(spanServiceName, spanProcess);
            }

            int spanByteSize = await this.GetSize(span).ConfigureAwait(false);

            if (spanByteSize + spanProcess.ByteSize > this.maxPacketSize)
            {
                throw new JaegerExporterException($"ThriftSender received a span that was too large, size = {spanByteSize + spanProcess.ByteSize}, max = {this.maxPacketSize}", null);
            }

            int spanTotalBytesNeeded = spanByteSize;
            if (!this.CurrentBatches.TryGetValue(spanServiceName, out var spanBatch))
            {
                spanBatch = new Batch(spanProcess);
                this.CurrentBatches.Add(spanServiceName, spanBatch);

                spanTotalBytesNeeded += spanProcess.ByteSize;
            }

            var flushedSpanCount = 0;

            await this.flushLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // flush if current batch size plus new span size equals or exceeds max batch size
                if (this.batchByteSize + spanTotalBytesNeeded >= this.maxPacketSize)
                {
                    flushedSpanCount = await this.FlushAsyncInternal(true, cancellationToken).ConfigureAwait(false);

                    // Flushing effectively erases the spanBatch we were working on, so we have to rebuild it.
                    spanBatch.Spans.Clear();
                    spanTotalBytesNeeded = spanByteSize + spanProcess.ByteSize;
                    this.CurrentBatches.Add(spanServiceName, spanBatch);
                }
                else
                {
                    this.maxFlushIntervalTimer.Enabled = true;
                }

                // add span to batch and wait for more spans
                spanBatch.Spans.Add(span);
                this.batchByteSize += spanTotalBytesNeeded;
            }
            finally
            {
                this.flushLock.Release();
            }

            return flushedSpanCount;
        }

        public Task<int> FlushAsync(CancellationToken cancellationToken) => this.FlushAsyncInternal(false, cancellationToken);

        public Task<int> CloseAsync(CancellationToken cancellationToken) => this.FlushAsyncInternal(false, cancellationToken);

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing).
            this.Dispose(true);
        }

        protected async Task SendAsync(IEnumerable<Batch> batches, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var batch in batches)
                {
                    await this.thriftClient.EmitBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                throw new JaegerExporterException($"Could not send {batches.Select(b => b.Spans.Count()).Sum()} spans", ex);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.maxFlushIntervalTimer.Dispose();
                    this.thriftClient.Dispose();
                    this.clientTransport.Dispose();
                }

                this.disposedValue = true;
            }
        }

        private async Task<int> FlushAsyncInternal(bool lockAlreadyHeld, CancellationToken cancellationToken)
        {
            if (!lockAlreadyHeld)
            {
                await this.flushLock.WaitAsync().ConfigureAwait(false);
            }

            try
            {
                this.maxFlushIntervalTimer.Enabled = false;

                int n = this.CurrentBatches.Values.Sum(b => b.Spans.Count);

                if (n == 0)
                {
                    return 0;
                }

                try
                {
                    await this.SendAsync(this.CurrentBatches.Values, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    this.CurrentBatches.Clear();
                    this.batchByteSize = 0;
                }

                return n;
            }
            finally
            {
                if (!lockAlreadyHeld)
                {
                    this.flushLock.Release();
                }
            }
        }

        private async Task<int> GetSize(TAbstractBase thriftBase)
        {
            using (var memoryTransport = new InMemoryTransport())
            {
                await thriftBase.WriteAsync(this.protocolFactory.GetProtocol(memoryTransport), CancellationToken.None).ConfigureAwait(false);
                return memoryTransport.GetBuffer().Length;
            }
        }
    }
}
