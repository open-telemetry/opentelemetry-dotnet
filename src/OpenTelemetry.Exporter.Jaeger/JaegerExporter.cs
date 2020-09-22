// <copyright file="JaegerExporter.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using System.Threading;
#if DEBUG
using System.Threading.Tasks;
#endif
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Thrift.Protocol;
using Thrift.Transport;

namespace OpenTelemetry.Exporter.Jaeger
{
    public class JaegerExporter : ActivityExporter
    {
        private readonly int maxPacketSize;
        private readonly TProtocolFactory protocolFactory;
        private readonly TTransport clientTransport;
        private readonly JaegerThriftClient thriftClient;
        private readonly InMemoryTransport memoryTransport;
        private readonly TProtocol memoryProtocol;
        private Dictionary<string, Process> processCache;
        private bool libraryResourceApplied;
        private int batchByteSize;
        private bool disposedValue; // To detect redundant dispose calls

        internal JaegerExporter(JaegerExporterOptions options, TTransport clientTransport = null)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.maxPacketSize = (!options.MaxPacketSize.HasValue || options.MaxPacketSize <= 0) ? JaegerExporterOptions.DefaultMaxPacketSize : options.MaxPacketSize.Value;
            this.protocolFactory = new TCompactProtocol.Factory();
            this.clientTransport = clientTransport ?? new JaegerThriftClientTransport(options.AgentHost, options.AgentPort);
            this.thriftClient = new JaegerThriftClient(this.protocolFactory.GetProtocol(this.clientTransport));
            this.memoryTransport = new InMemoryTransport(16000);
            this.memoryProtocol = this.protocolFactory.GetProtocol(this.memoryTransport);

            this.Process = new Process(options.ServiceName, options.ProcessTags);
        }

        public Process Process { get; internal set; }

        internal Dictionary<string, Batch> CurrentBatches { get; } = new Dictionary<string, Batch>();

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            try
            {
                foreach (var activity in activityBatch)
                {
                    if (!this.libraryResourceApplied)
                    {
                        var libraryResource = activity.GetResource();

                        this.ApplyLibraryResource(libraryResource ?? Resource.Empty);

                        this.libraryResourceApplied = true;
                    }

                    this.AppendSpan(activity.ToJaegerSpan());
                }

                this.SendCurrentBatches();

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                JaegerExporterEventSource.Log.FailedExport(ex);

                return ExportResult.Failure;
            }
        }

        internal void ApplyLibraryResource(Resource libraryResource)
        {
            if (libraryResource is null)
            {
                throw new ArgumentNullException(nameof(libraryResource));
            }

            var process = this.Process;

            string serviceName = null;
            string serviceNamespace = null;
            foreach (var label in libraryResource.Attributes)
            {
                string key = label.Key;

                if (label.Value is string strVal)
                {
                    switch (key)
                    {
                        case Resource.ServiceNameKey:
                            serviceName = strVal;
                            continue;
                        case Resource.ServiceNamespaceKey:
                            serviceNamespace = strVal;
                            continue;
                        case Resource.LibraryNameKey:
                        case Resource.LibraryVersionKey:
                            continue;
                    }
                }

                if (process.Tags == null)
                {
                    process.Tags = new Dictionary<string, JaegerTag>();
                }

                process.Tags[label.Key] = label.ToJaegerTag();
            }

            if (serviceName != null)
            {
                process.ServiceName = serviceNamespace != null
                    ? serviceNamespace + "." + serviceName
                    : serviceName;
            }

            if (string.IsNullOrEmpty(process.ServiceName))
            {
                process.ServiceName = JaegerExporterOptions.DefaultServiceName;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendSpan(JaegerSpan jaegerSpan)
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

            var spanTotalBytesNeeded = spanMessage.Count;

            if (!this.CurrentBatches.TryGetValue(spanServiceName, out var spanBatch))
            {
                spanBatch = new Batch(spanProcess);
                this.CurrentBatches.Add(spanServiceName, spanBatch);

                spanTotalBytesNeeded += spanProcess.Message.Length;
            }

            if (this.batchByteSize + spanTotalBytesNeeded >= this.maxPacketSize)
            {
                this.SendCurrentBatches();

                // Flushing effectively erases the spanBatch we were working on, so we have to rebuild it.
                spanBatch.Clear();
                spanTotalBytesNeeded = spanMessage.Count + spanProcess.Message.Length;
                this.CurrentBatches.Add(spanServiceName, spanBatch);
            }

            spanBatch.Add(spanMessage);
            this.batchByteSize += spanTotalBytesNeeded;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.thriftClient.Dispose();
                    this.clientTransport.Dispose();
                    this.memoryTransport.Dispose();
                    this.memoryProtocol.Dispose();
                }

                this.disposedValue = true;
            }

            base.Dispose(disposing);
        }

        private void SendCurrentBatches()
        {
            try
            {
                foreach (var batch in this.CurrentBatches)
                {
                    var task = this.thriftClient.WriteBatchAsync(batch.Value, CancellationToken.None);
#if DEBUG
                    if (task.Status != TaskStatus.RanToCompletion)
                    {
                        throw new InvalidOperationException();
                    }
#endif
                    batch.Value.Return();
                }
            }
            finally
            {
                this.CurrentBatches.Clear();
                this.batchByteSize = 0;
                this.memoryTransport.Reset();
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
