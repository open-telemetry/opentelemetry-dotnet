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
using Process = OpenTelemetry.Exporter.Jaeger.Implementation.Process;

namespace OpenTelemetry.Exporter
{
    public class JaegerExporter : BaseExporter<Activity>
    {
        private readonly int maxPayloadSizeInBytes;
        private readonly IJaegerClient client;
        private readonly TProtocol batchWriter;
        private readonly TProtocol spanWriter;
        private readonly bool sendUsingEmitBatchArgs;
        private int minimumBatchSizeInBytes;
        private int currentBatchSizeInBytes;
        private uint numberOfSpansInCurrentBatch;
        private uint sequenceId;
        private bool disposed;

        public JaegerExporter(JaegerExporterOptions options)
            : this(options, null)
        {
        }

        internal JaegerExporter(JaegerExporterOptions options, TProtocolFactory protocolFactory = null, IJaegerClient client = null)
        {
            Guard.Null(options, nameof(options));

            this.maxPayloadSizeInBytes = (!options.MaxPayloadSizeInBytes.HasValue || options.MaxPayloadSizeInBytes <= 0)
                ? JaegerExporterOptions.DefaultMaxPayloadSizeInBytes
                : options.MaxPayloadSizeInBytes.Value;

            if (options.Protocol == JaegerExportProtocol.UdpCompactThrift)
            {
                protocolFactory ??= new TCompactProtocol.Factory();
                client ??= new JaegerUdpClient(options.AgentHost, options.AgentPort);
                this.sendUsingEmitBatchArgs = true;
            }
            else if (options.Protocol == JaegerExportProtocol.HttpBinaryThrift)
            {
                protocolFactory ??= new TBinaryProtocol.Factory(strictRead: false, strictWrite: false);
                client ??= new JaegerHttpClient(
                    options.Endpoint,
                    options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("JaegerExporterOptions was missing HttpClientFactory or it returned null."));
            }
            else
            {
                throw new NotSupportedException();
            }

            this.client = client;
            this.batchWriter = protocolFactory.GetProtocol(16384);
            this.spanWriter = protocolFactory.GetProtocol(4096);

            string serviceName = (string)this.ParentProvider.GetDefaultResource().Attributes.FirstOrDefault(
                pair => pair.Key == ResourceSemanticConventions.AttributeServiceName).Value;
            this.Process = new Process(serviceName);

            client.Connect();
        }

        internal Process Process { get; set; }

        internal EmitBatchArgs EmitBatchArgs { get; private set; }

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
                    var jaegerSpan = activity.ToJaegerSpan();
                    this.AppendSpan(jaegerSpan);
                    jaegerSpan.Return();
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

            this.Batch = new Batch(this.Process, this.batchWriter);
            if (this.sendUsingEmitBatchArgs)
            {
                this.EmitBatchArgs = new EmitBatchArgs(this.batchWriter);
                this.Batch.SpanCountPosition += this.EmitBatchArgs.EmitBatchArgsBeginMessage.Length;
            }

            this.minimumBatchSizeInBytes = this.EmitBatchArgs?.MinimumMessageSize ?? 0
                + this.Batch.MinimumMessageSize;
            this.ResetBatch();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AppendSpan(JaegerSpan jaegerSpan)
        {
            jaegerSpan.Write(this.spanWriter);
            try
            {
                var spanTotalBytesNeeded = this.spanWriter.Length;

                if (this.numberOfSpansInCurrentBatch > 0
                    && this.currentBatchSizeInBytes + spanTotalBytesNeeded >= this.maxPayloadSizeInBytes)
                {
                    this.SendCurrentBatch();
                }

                var spanData = this.spanWriter.WrittenData;
                this.batchWriter.WriteRaw(spanData);

                this.numberOfSpansInCurrentBatch++;
                this.currentBatchSizeInBytes += spanTotalBytesNeeded;
            }
            finally
            {
                this.spanWriter.Clear();
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.client.Dispose();
                    this.batchWriter.Dispose();
                    this.spanWriter.Dispose();
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
                this.batchWriter.WriteRaw(this.Batch.BatchEndMessage);

                if (this.sendUsingEmitBatchArgs)
                {
                    this.batchWriter.WriteRaw(this.EmitBatchArgs.EmitBatchArgsEndMessage);

                    this.batchWriter.Position = this.EmitBatchArgs.SeqIdPosition;
                    this.batchWriter.WriteUI32(this.sequenceId++);
                }

                this.batchWriter.Position = this.Batch.SpanCountPosition;
                this.batchWriter.WriteUI32(this.numberOfSpansInCurrentBatch);

                var writtenData = this.batchWriter.WrittenData;

                this.client.Send(writtenData.Array, writtenData.Offset, writtenData.Count);
            }
            finally
            {
                this.batchWriter.Clear();
                this.ResetBatch();
            }
        }

        private void ResetBatch()
        {
            this.currentBatchSizeInBytes = this.minimumBatchSizeInBytes;
            this.numberOfSpansInCurrentBatch = 0;

            if (this.sendUsingEmitBatchArgs)
            {
                this.batchWriter.WriteRaw(this.EmitBatchArgs.EmitBatchArgsBeginMessage);
            }

            this.batchWriter.WriteRaw(this.Batch.BatchBeginMessage);
        }
    }
}
