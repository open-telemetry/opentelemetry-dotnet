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
        internal uint NumberOfSpansInCurrentBatch;

        private readonly byte[] uInt32Storage = new byte[8];
        private readonly int maxPayloadSizeInBytes;
        private readonly IJaegerClient client;
        private readonly TProtocol batchWriter;
        private readonly TProtocol spanWriter;
        private readonly bool sendUsingEmitBatchArgs;
        private int minimumBatchSizeInBytes;
        private int currentBatchSizeInBytes;
        private int spanStartPosition;
        private uint sequenceId;
        private bool disposed;

        public JaegerExporter(JaegerExporterOptions options)
            : this(options, null)
        {
        }

        internal JaegerExporter(JaegerExporterOptions options, TProtocolFactory protocolFactory = null, IJaegerClient client = null)
        {
            Guard.ThrowIfNull(options);

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
                protocolFactory ??= new TBinaryProtocol.Factory();
                client ??= new JaegerHttpClient(
                    options.Endpoint,
                    options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("JaegerExporterOptions was missing HttpClientFactory or it returned null."));
            }
            else
            {
                throw new NotSupportedException();
            }

            this.client = client;
            this.batchWriter = protocolFactory.GetProtocol(this.maxPayloadSizeInBytes * 2);
            this.spanWriter = protocolFactory.GetProtocol(this.maxPayloadSizeInBytes);

            this.Process = new();

            client.Connect();
        }

        internal Process Process { get; }

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
            Guard.ThrowIfNull(resource);

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

                if (JaegerTagTransformer.Instance.TryTransformTag(label, out var result))
                {
                    if (process.Tags == null)
                    {
                        process.Tags = new Dictionary<string, JaegerTag>();
                    }

                    process.Tags[key] = result;
                }
            }

            if (!string.IsNullOrWhiteSpace(serviceName))
            {
                serviceName = string.IsNullOrEmpty(serviceNamespace)
                    ? serviceName
                    : serviceNamespace + "." + serviceName;
            }
            else
            {
                serviceName = (string)this.ParentProvider.GetDefaultResource().Attributes.FirstOrDefault(
                    pair => pair.Key == ResourceSemanticConventions.AttributeServiceName).Value;
            }

            process.ServiceName = serviceName;

            this.Batch = new Batch(process, this.batchWriter);
            if (this.sendUsingEmitBatchArgs)
            {
                this.EmitBatchArgs = new EmitBatchArgs(this.batchWriter);
                this.Batch.SpanCountPosition += this.EmitBatchArgs.EmitBatchArgsBeginMessage.Length;
                this.batchWriter.WriteRaw(this.EmitBatchArgs.EmitBatchArgsBeginMessage);
            }

            this.batchWriter.WriteRaw(this.Batch.BatchBeginMessage);
            this.spanStartPosition = this.batchWriter.Position;

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

                if (this.NumberOfSpansInCurrentBatch > 0
                    && this.currentBatchSizeInBytes + spanTotalBytesNeeded >= this.maxPayloadSizeInBytes)
                {
                    this.SendCurrentBatch();
                }

                var spanData = this.spanWriter.WrittenData;
                this.batchWriter.WriteRaw(spanData);

                this.NumberOfSpansInCurrentBatch++;
                this.currentBatchSizeInBytes += spanTotalBytesNeeded;
            }
            finally
            {
                this.spanWriter.Clear();
            }
        }

        internal void SendCurrentBatch()
        {
            try
            {
                this.batchWriter.WriteRaw(this.Batch.BatchEndMessage);

                if (this.sendUsingEmitBatchArgs)
                {
                    this.batchWriter.WriteRaw(this.EmitBatchArgs.EmitBatchArgsEndMessage);

                    this.WriteUInt32AtPosition(this.EmitBatchArgs.SeqIdPosition, ++this.sequenceId);
                }

                this.WriteUInt32AtPosition(this.Batch.SpanCountPosition, this.NumberOfSpansInCurrentBatch);

                var writtenData = this.batchWriter.WrittenData;

                this.client.Send(writtenData.Array, writtenData.Offset, writtenData.Count);
            }
            finally
            {
                this.ResetBatch();
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    try
                    {
                        this.client.Close();
                    }
                    catch
                    {
                    }

                    this.client.Dispose();
                    this.batchWriter.Dispose();
                    this.spanWriter.Dispose();
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteUInt32AtPosition(int position, uint value)
        {
            this.batchWriter.Position = position;
            int numberOfBytes = this.batchWriter.WriteUI32(value, this.uInt32Storage);
            this.batchWriter.WriteRaw(this.uInt32Storage, 0, numberOfBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResetBatch()
        {
            this.currentBatchSizeInBytes = this.minimumBatchSizeInBytes;
            this.NumberOfSpansInCurrentBatch = 0;
            this.batchWriter.Clear(this.spanStartPosition);
        }
    }
}
