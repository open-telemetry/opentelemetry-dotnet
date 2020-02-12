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
        private readonly List<JaegerSpan> currentBatch = new List<JaegerSpan>();

        private readonly SemaphoreSlim flushLock = new SemaphoreSlim(1);
        private readonly TimeSpan maxFlushInterval;
        private readonly System.Timers.Timer maxFlushIntervalTimer;

        private int? processByteSize;
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
                await this.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            };
        }

        public Process Process { get; private set; }

        public async Task<int> AppendAsync(JaegerSpan span, CancellationToken cancellationToken)
        {
            if (!this.processByteSize.HasValue)
            {
                this.processByteSize = await this.GetSize(this.Process).ConfigureAwait(false);
                this.batchByteSize = this.processByteSize.Value;
            }

            int spanSize = await this.GetSize(span).ConfigureAwait(false);

            if (spanSize + this.processByteSize > this.maxPacketSize)
            {
                throw new JaegerExporterException($"ThriftSender received a span that was too large, size = {spanSize + this.processByteSize}, max = {this.maxPacketSize}", null);
            }

            var flushedSpanCount = 0;

            try
            {
                await this.flushLock.WaitAsync().ConfigureAwait(false);

                // flush if current batch size plus new span size equals or exceeds max batch size
                if (this.batchByteSize + spanSize >= this.maxPacketSize)
                {
                    flushedSpanCount = await this.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    this.maxFlushIntervalTimer.Enabled = true;
                }

                // add span to batch and wait for more spans
                this.currentBatch.Add(span);
                this.batchByteSize += spanSize;
            }
            finally
            {
                this.flushLock.Release();
            }

            return flushedSpanCount;
        }

        public async Task<int> FlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.flushLock.WaitAsync().ConfigureAwait(false);

                this.maxFlushIntervalTimer.Enabled = false;

                int n = this.currentBatch.Count;

                if (n == 0)
                {
                    return 0;
                }

                try
                {
                    await this.SendAsync(this.Process, this.currentBatch, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    this.currentBatch.Clear();
                    this.batchByteSize = this.processByteSize.Value;
                }

                return n;
            }
            finally
            {
                this.flushLock.Release();
            }
        }

        public virtual Task<int> CloseAsync(CancellationToken cancellationToken) => this.FlushAsync(cancellationToken);

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing).
            this.Dispose(true);
        }

        protected async Task SendAsync(Process process, List<JaegerSpan> spans, CancellationToken cancellationToken)
        {
            try
            {
                var batch = new Batch(process, spans);
                await this.thriftClient.EmitBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new JaegerExporterException($"Could not send {spans.Count} spans", ex);
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
