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
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Resources;
using Thrift.Protocol;
using Thrift.Transport;
using Process = OpenTelemetry.Exporter.Jaeger.Implementation.Process;

namespace OpenTelemetry.Exporter.Jaeger
{
    internal class JaegerExporter : BaseExporter<Activity>
    {
        private readonly int maxPayloadSizeInBytes;
        private readonly TProtocolFactory protocolFactory;
        private readonly TTransport clientTransport;
        private readonly JaegerThriftClient thriftClient;
        private readonly InMemoryTransport memoryTransport;
        private readonly TProtocol memoryProtocol;
        private Dictionary<string, Process> processCache;
        private int batchByteSize;
        private bool disposedValue; // To detect redundant dispose calls

        internal JaegerExporter(JaegerExporterOptions options, TTransport clientTransport = null)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            this.maxPayloadSizeInBytes = (!options.MaxPayloadSizeInBytes.HasValue || options.MaxPayloadSizeInBytes <= 0) ? JaegerExporterOptions.DefaultMaxPayloadSizeInBytes : options.MaxPayloadSizeInBytes.Value;
            this.protocolFactory = new TCompactProtocol.Factory();
            this.clientTransport = clientTransport ?? new JaegerThriftClientTransport(options.AgentHost, options.AgentPort);
            this.thriftClient = new JaegerThriftClient(this.protocolFactory.GetProtocol(this.clientTransport));
            this.memoryTransport = new InMemoryTransport(16000);
            this.memoryProtocol = this.protocolFactory.GetProtocol(this.memoryTransport);

            this.Process = new Process(options.ServiceName, options.ProcessTags);
        }

        internal Process Process { get; set; }

        internal Dictionary<string, Batch> CurrentBatches { get; } = new Dictionary<string, Batch>();

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            try
            {
                if (this.processCache == null)
                {
                    this.SetResource(this.ParentProvider.GetResource());
                }

                foreach (var activity in activityBatch)
                {
                    this.AppendSpan(activity.ToJaegerSpan());
                }

                this.SendCurrentBatches(null);

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                JaegerExporterEventSource.Log.FailedExport(ex);

                return ExportResult.Failure;
            }
        }

        internal void SetResource(Resource resource)
        {
            if (resource is null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            var process = this.Process;

            string serviceName = null;
            string serviceNamespace = null;
            foreach (var label in resource.Attributes)
            {
                string key = label.Key;

                if (label.Value is string strVal)
                {
                    switch (key)
                    {
                        case ResourceSemanticConventions.AttributeServiceName:
                            serviceName = strVal;
                            continue;
                        case ResourceSemanticConventions.AttributeServiceNamespace:
                            serviceNamespace = strVal;
                            continue;
                    }
                }

                if (process.Tags == null)
                {
                    process.Tags = new Dictionary<string, JaegerTag>();
                }

                process.Tags[key] = label.ToJaegerTag();
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

            this.Process.Message = this.BuildThriftMessage(this.Process).ToArray();
            this.processCache = new Dictionary<string, Process>
            {
                [this.Process.ServiceName] = this.Process,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendSpan(JaegerSpan jaegerSpan)
        {
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

            if (this.batchByteSize + spanTotalBytesNeeded >= this.maxPayloadSizeInBytes)
            {
                this.SendCurrentBatches(spanBatch);

                // Flushing effectively erases the spanBatch we were working on, so we have to rebuild it.
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

        private void SendCurrentBatches(Batch workingBatch)
        {
            try
            {
                foreach (var batchKvp in this.CurrentBatches)
                {
                    var batch = batchKvp.Value;

                    this.thriftClient.SendBatch(batch);

                    if (batch != workingBatch)
                    {
                        batch.Return();
                    }
                    else
                    {
                        batch.Clear();
                    }
                }
            }
            finally
            {
                this.CurrentBatches.Clear();
                this.batchByteSize = 0;
                this.memoryTransport.Reset();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BufferWriterMemory BuildThriftMessage(Process process)
        {
            process.Write(this.memoryProtocol);

            return this.memoryTransport.ToBuffer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private BufferWriterMemory BuildThriftMessage(in JaegerSpan jaegerSpan)
        {
            jaegerSpan.Write(this.memoryProtocol);

            return this.memoryTransport.ToBuffer();
        }
    }
}
