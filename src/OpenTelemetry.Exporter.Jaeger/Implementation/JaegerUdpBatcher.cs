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
        private readonly List<PooledByteBufferWriter> currentBatch = new List<PooledByteBufferWriter>();

        private readonly SemaphoreSlim flushLock = new SemaphoreSlim(1);
        private readonly TimeSpan maxFlushInterval;
        private readonly System.Timers.Timer maxFlushIntervalTimer;

        private ArraySegment<byte>? processMessage;
        private int processByteSize;
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
            if (!this.processMessage.HasValue)
            {
                this.processMessage = (await this.BuildThriftMessage(this.Process).ConfigureAwait(false)).ToArraySegment();
                this.processByteSize = this.processMessage.Value.Count;
                this.batchByteSize = this.processByteSize;
            }

            var spanMessage = await this.BuildThriftMessage(span).ConfigureAwait(false);

            if (spanMessage.WrittenCount + this.processByteSize > this.maxPacketSize)
            {
                throw new JaegerExporterException($"ThriftSender received a span that was too large, size = {spanMessage.WrittenCount + this.processByteSize}, max = {this.maxPacketSize}", null);
            }

            var flushedSpanCount = 0;

            try
            {
                await this.flushLock.WaitAsync().ConfigureAwait(false);

                // flush if current batch size plus new span size equals or exceeds max batch size
                if (this.batchByteSize + spanMessage.WrittenCount >= this.maxPacketSize)
                {
                    flushedSpanCount = await this.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    this.maxFlushIntervalTimer.Enabled = true;
                }

                // add span to batch and wait for more spans
                this.currentBatch.Add(spanMessage);
                this.batchByteSize += spanMessage.WrittenCount;
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
                    await this.SendAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    foreach (var p in this.currentBatch)
                    {
                        p.Dispose();
                    }

                    this.currentBatch.Clear();
                    this.batchByteSize = this.processByteSize;
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

        protected async Task SendAsync(CancellationToken cancellationToken)
        {
            try
            {
                var batch = new Batch(this.processMessage.Value, this.currentBatch.Select(p => p.ToArraySegment()));
                await this.thriftClient.EmitBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new JaegerExporterException($"Could not send {this.currentBatch.Count} spans", ex);
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

#if NETSTANDARD2_1
        private async ValueTask<PooledByteBufferWriter> BuildThriftMessage(TAbstractBase thriftBase, int hintSize = 1024)
#else
        private async Task<PooledByteBufferWriter> BuildThriftMessage(TAbstractBase thriftBase, int hintSize = 1024)
#endif
        {
            var buffer = new PooledByteBufferWriter(hintSize);

            using (var memoryTransport = new InMemoryTransport(buffer))
            {
                await thriftBase.WriteAsync(this.protocolFactory.GetProtocol(memoryTransport), CancellationToken.None).ConfigureAwait(false);
            }

            return buffer;
        }
    }
}
