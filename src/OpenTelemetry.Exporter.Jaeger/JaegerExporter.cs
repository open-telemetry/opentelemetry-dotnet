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
using System.Linq;
using System.Runtime.CompilerServices;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using Thrift.Protocol;
using Thrift.Transport;
using Process = OpenTelemetry.Exporter.Jaeger.Implementation.Process;

namespace OpenTelemetry.Exporter
{
    public class JaegerExporter : BaseExporter<Activity>
    {
        private readonly int maxPayloadSizeInBytes;
        private readonly TProtocolFactory protocolFactory;
        private readonly TTransport clientTransport;
        private readonly JaegerThriftClient thriftClient;
        private readonly InMemoryTransport memoryTransport;
        private readonly TProtocol memoryProtocol;
        private int batchByteSize;
        private bool disposed;

        public JaegerExporter(JaegerExporterOptions options)
            : this(options, null)
        {
        }

        internal JaegerExporter(JaegerExporterOptions options, TTransport clientTransport = null)
        {
            Guard.Null(options, nameof(options));

            this.maxPayloadSizeInBytes = (!options.MaxPayloadSizeInBytes.HasValue || options.MaxPayloadSizeInBytes <= 0) ? JaegerExporterOptions.DefaultMaxPayloadSizeInBytes : options.MaxPayloadSizeInBytes.Value;
            this.protocolFactory = new TCompactProtocol.Factory();
            this.clientTransport = clientTransport ?? new JaegerThriftClientTransport(options.AgentHost, options.AgentPort);
            this.thriftClient = new JaegerThriftClient(this.protocolFactory.GetProtocol(this.clientTransport));
            this.memoryTransport = new InMemoryTransport(16000);
            this.memoryProtocol = this.protocolFactory.GetProtocol(this.memoryTransport);

            string serviceName = (string)this.ParentProvider.GetDefaultResource().Attributes.Where(
                    pair => pair.Key == ResourceSemanticConventions.AttributeServiceName).FirstOrDefault().Value;
            this.Process = new Process(serviceName);
        }

        internal Process Process { get; set; }

        internal Batch Batch { get; private set; }

        /// <inheritdoc/>
        public override ExportResult Export(in Batch<Activity> activityBatch)
        {
            try
            {
                if (this.Batch == null)
                {
                    this.SetResourceAndInitializeBatch(this.ParentProvider.GetResource());
                }

                foreach (var activity in activityBatch)
                {
                    this.AppendSpan(activity.ToJaegerSpan());
                }

                this.SendCurrentBatch();

                return ExportResult.Success;
            }
            catch (Exception ex)
            {
                JaegerExporterEventSource.Log.FailedExport(ex);

                return ExportResult.Failure;
            }
        }

        internal void SetResourceAndInitializeBatch(Resource resource)
        {
            Guard.Null(resource, nameof(resource));

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
                serviceName = string.IsNullOrEmpty(serviceNamespace)
                    ? serviceName
                    : serviceNamespace + "." + serviceName;
            }

            if (!string.IsNullOrEmpty(serviceName))
            {
                process.ServiceName = serviceName;
            }

            this.Process.Message = this.BuildThriftMessage(this.Process).ToArray();
            this.Batch = new Batch(this.Process);
            this.batchByteSize = this.Process.Message.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendSpan(JaegerSpan jaegerSpan)
        {
            var spanMessage = this.BuildThriftMessage(jaegerSpan);

            jaegerSpan.Return();

            var spanTotalBytesNeeded = spanMessage.Count;

            if (this.batchByteSize + spanTotalBytesNeeded >= this.maxPayloadSizeInBytes)
            {
                this.SendCurrentBatch();

                // SendCurrentBatch clears/invalidates the BufferWriter in InMemoryTransport.
                // The new spanMessage is still located in it, though. It might get overwritten later
                // when spans are written in the buffer for the next batch.
                // Move spanMessage to the beginning of the BufferWriter to avoid data corruption.
                this.memoryTransport.Write(spanMessage.BufferWriter.Buffer, spanMessage.Offset, spanMessage.Count);
                spanMessage = this.memoryTransport.ToBuffer();
            }

            this.Batch.Add(spanMessage);
            this.batchByteSize += spanTotalBytesNeeded;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.thriftClient.Dispose();
                    this.clientTransport.Dispose();
                    this.memoryTransport.Dispose();
                    this.memoryProtocol.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SendCurrentBatch()
        {
            try
            {
                this.thriftClient.SendBatch(this.Batch);
            }
            finally
            {
                this.Batch.Clear();
                this.batchByteSize = this.Process.Message.Length;
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
