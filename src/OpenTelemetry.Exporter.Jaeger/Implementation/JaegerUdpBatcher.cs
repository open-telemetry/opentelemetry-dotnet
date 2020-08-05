// <copyright file="JaegerUdpBatcher.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocol;
using Thrift.Transport;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class JaegerUdpBatcher : IDisposable
    {
        private readonly int maxPacketSize;
        private readonly TProtocolFactory protocolFactory;
        private readonly TTransport clientTransport;
        private readonly JaegerThriftClient thriftClient;
        private readonly InMemoryTransport memoryTransport;
        private readonly TProtocol memoryProtocol;
        private readonly SemaphoreSlim flushLock = new SemaphoreSlim(1);
        private readonly TimeSpan maxFlushInterval;
        private readonly System.Timers.Timer maxFlushIntervalTimer;
        private Dictionary<string, Process> processCache;
        private int batchByteSize;
        private bool disposed;

        public JaegerUdpBatcher(JaegerExporterOptions options, TTransport clientTransport = null)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.MaxFlushInterval <= TimeSpan.Zero)
            {
                options.MaxFlushInterval = TimeSpan.FromSeconds(10);
            }

            this.maxPacketSize = (!options.MaxPacketSize.HasValue || options.MaxPacketSize <= 0) ? JaegerExporterOptions.DefaultMaxPacketSize : options.MaxPacketSize.Value;
            this.protocolFactory = new TCompactProtocol.Factory();
            this.clientTransport = clientTransport ?? new JaegerThriftClientTransport(options.AgentHost, options.AgentPort);
            this.thriftClient = new JaegerThriftClient(this.protocolFactory.GetProtocol(this.clientTransport));
            this.memoryTransport = new InMemoryTransport(16000);
            this.memoryProtocol = this.protocolFactory.GetProtocol(this.memoryTransport);

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
                bool lockTaken = this.flushLock.Wait(0);
                try
                {
                    if (!lockTaken)
                    {
                        // If the lock was already held, it means a flush is already executing.
                        return;
                    }

                    await this.FlushAsyncInternal(lockAlreadyHeld: true, CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    if (lockTaken)
                    {
                        this.flushLock.Release();
                    }
                }
            };
        }

        public Process Process { get; internal set; }

        internal Dictionary<string, Batch> CurrentBatches { get; } = new Dictionary<string, Batch>();

        public async ValueTask<int> AppendBatchAsync(IEnumerable<Activity> activityBatch, CancellationToken cancellationToken)
        {
            await this.flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                int recordsFlushed = 0;

                foreach (var activity in activityBatch)
                {
                    recordsFlushed += await this.AppendInternalAsync(activity.ToJaegerSpan(), cancellationToken).ConfigureAwait(false);
                }

                return recordsFlushed;
            }
            finally
            {
                this.flushLock.Release();
            }
        }

        public async ValueTask<int> AppendAsync(Activity activity, CancellationToken cancellationToken)
        {
            await this.flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await this.AppendInternalAsync(activity.ToJaegerSpan(), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                this.flushLock.Release();
            }
        }

        public ValueTask<int> FlushAsync(CancellationToken cancellationToken) => this.FlushAsyncInternal(lockAlreadyHeld: false, cancellationToken);

        public ValueTask<int> CloseAsync(CancellationToken cancellationToken) => this.FlushAsyncInternal(lockAlreadyHeld: false, cancellationToken);

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing).
            this.Dispose(true);
        }

        protected async ValueTask<int> AppendInternalAsync(JaegerSpan jaegerSpan, CancellationToken cancellationToken)
        {
            if (this.processCache == null)
            {
                this.Process.Message = this.BuildThriftMessage(this.Process).ToArray();
                this.processCache = new Dictionary<string, Process>
                {
                    [this.Process.ServiceName] = this.Process,
                };
            }

            var spanServiceName = jaegerSpan.PeerServiceName ?? this.Process.ServiceName;

            if (!this.processCache.TryGetValue(spanServiceName, out var spanProcess))
            {
                spanProcess = new Process(spanServiceName, this.Process.Tags);
                spanProcess.Message = this.BuildThriftMessage(spanProcess).ToArray();
                this.processCache.Add(spanServiceName, spanProcess);
            }

            var spanMessage = this.BuildThriftMessage(jaegerSpan);

            jaegerSpan.Return();

            if (spanMessage.Count + spanProcess.Message.Length > this.maxPacketSize)
            {
                throw new JaegerExporterException($"ThriftSender received a span that was too large, size = {spanMessage.Count + spanProcess.Message.Length}, max = {this.maxPacketSize}", null);
            }

            var spanTotalBytesNeeded = spanMessage.Count;

            var flushedSpanCount = 0;

            if (!this.CurrentBatches.TryGetValue(spanServiceName, out var spanBatch))
            {
                spanBatch = new Batch(spanProcess)
                {
                    SpanMessages = new List<BufferWriterMemory>(),
                };
                this.CurrentBatches.Add(spanServiceName, spanBatch);

                spanTotalBytesNeeded += spanProcess.Message.Length;
            }

            // flush if current batch size plus new span size equals or exceeds max batch size
            if (this.batchByteSize + spanTotalBytesNeeded >= this.maxPacketSize)
            {
                flushedSpanCount = await this.FlushAsyncInternal(lockAlreadyHeld: true, cancellationToken).ConfigureAwait(false);

                // Flushing effectively erases the spanBatch we were working on, so we have to rebuild it.
                spanBatch.SpanMessages.Clear();
                spanTotalBytesNeeded = spanMessage.Count + spanProcess.Message.Length;
                this.CurrentBatches.Add(spanServiceName, spanBatch);
            }
            else
            {
                this.maxFlushIntervalTimer.Enabled = true;
            }

            // add span to batch and wait for more spans
            spanBatch.SpanMessages.Add(spanMessage);
            this.batchByteSize += spanTotalBytesNeeded;

            return flushedSpanCount;
        }

        protected async Task SendAsync(Dictionary<string, Batch> batches, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var batch in batches)
                {
                    await this.thriftClient.WriteBatchAsync(
                        batch.Value.Process.Message,
                        batch.Value.SpanMessages,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                throw new JaegerExporterException($"Could not send {batches.Select(b => b.Value.SpanMessages.Count).Sum()} spans", ex);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                this.CloseAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
            }

            if (disposing && !this.disposed)
            {
                this.maxFlushIntervalTimer.Dispose();
                this.thriftClient.Dispose();
                this.clientTransport.Dispose();
                this.memoryProtocol.Dispose();
                this.flushLock.Dispose();

                this.disposed = true;
            }
        }

        private async ValueTask<int> FlushAsyncInternal(bool lockAlreadyHeld, CancellationToken cancellationToken)
        {
            if (!lockAlreadyHeld)
            {
                await this.flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                this.maxFlushIntervalTimer.Enabled = false;

                int n = this.CurrentBatches.Sum(b => b.Value.SpanMessages.Count);

                if (n == 0)
                {
                    return 0;
                }

                try
                {
                    await this.SendAsync(this.CurrentBatches, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    this.CurrentBatches.Clear();
                    this.batchByteSize = 0;
                    this.memoryTransport.Reset();
                }

                return n;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                JaegerExporterEventSource.Log.FailedFlush(ex);

                return 0;
            }
            finally
            {
                if (!lockAlreadyHeld)
                {
                    this.flushLock.Release();
                }
            }
        }

        private BufferWriterMemory BuildThriftMessage(Process process)
        {
            var task = process.WriteAsync(this.memoryProtocol, CancellationToken.None);
#if DEBUG
            if (task.Status != TaskStatus.RanToCompletion)
            {
                throw new InvalidOperationException();
            }
#endif
            return this.memoryTransport.ToBuffer();
        }

        // Prevents boxing of JaegerSpan struct.
        private BufferWriterMemory BuildThriftMessage(in JaegerSpan jaegerSpan)
        {
            var task = jaegerSpan.WriteAsync(this.memoryProtocol, CancellationToken.None);
#if DEBUG
            if (task.Status != TaskStatus.RanToCompletion)
            {
                throw new InvalidOperationException();
            }
#endif
            return this.memoryTransport.ToBuffer();
        }
    }
}
