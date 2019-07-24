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

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Thrift.Protocols;

    public class JaegerUdpBatcher : IJaegerUdpBatcher
    {
        private const int DefaultMaxPacketSize = 65000;
        private readonly int? maxPacketSize;
        private readonly ITProtocolFactory protocolFactory;
        private readonly JaegerThriftClientTransport clientTransport;
        private readonly JaegerThriftClient thriftClient;
        private readonly Process process;
        private readonly int processByteSize;
        private readonly List<JaegerSpan> currentBatch = new List<JaegerSpan>();

        private int batchByteSize;

        private bool disposedValue = false; // To detect redundant calls

        public JaegerUdpBatcher(JaegerExporterOptions options)
        {
            this.maxPacketSize = options.MaxPacketSize == 0 ? DefaultMaxPacketSize : options.MaxPacketSize;
            this.protocolFactory = new TCompactProtocol.Factory();
            this.clientTransport = new JaegerThriftClientTransport(options.AgentHost, options.AgentPort.Value);
            this.thriftClient = new JaegerThriftClient(this.protocolFactory.GetProtocol(this.clientTransport));
            this.process = new Process(options.ServiceName, options.ProcessTags);
            this.processByteSize = this.GetSize(this.process);
            this.batchByteSize = this.processByteSize;
        }

        public async Task<int> AppendAsync(JaegerSpan span, CancellationToken cancellationToken)
        {
            int spanSize = this.GetSize(span);

            if (spanSize > this.maxPacketSize)
            {
                throw new JaegerExporterException($"ThriftSender received a span that was too large, size = {spanSize}, max = {this.maxPacketSize}", null);
            }

            this.batchByteSize += spanSize;
            if (this.batchByteSize <= this.maxPacketSize)
            {
                this.currentBatch.Add(span);

                if (this.batchByteSize < this.maxPacketSize)
                {
                    return 0;
                }

                return await this.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            int n;

            try
            {
                n = await this.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (JaegerExporterException ex)
            {
                // +1 for the span not submitted in the buffer above
                throw new JaegerExporterException(ex.Message, ex);
            }

            this.currentBatch.Add(span);
            this.batchByteSize = this.processByteSize + spanSize;
            return n;
        }

        public async Task<int> FlushAsync(CancellationToken cancellationToken)
        {
            int n = this.currentBatch.Count;

            if (n == 0)
            {
                return 0;
            }

            try
            {
                await this.SendAsync(this.process, this.currentBatch, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                this.currentBatch.Clear();
                this.batchByteSize = this.processByteSize;
            }

            return n;
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
                    this.thriftClient.Dispose();
                    this.clientTransport.Dispose();
                }

                this.disposedValue = true;
            }
        }

        private int GetSize(TAbstractBase thriftBase)
        {
            using (var memoryTransport = new InMemoryTransport())
            {
                thriftBase.WriteAsync(this.protocolFactory.GetProtocol(memoryTransport), CancellationToken.None).GetAwaiter().GetResult();
                return memoryTransport.GetBuffer().Length;
            }
        }
    }
}
