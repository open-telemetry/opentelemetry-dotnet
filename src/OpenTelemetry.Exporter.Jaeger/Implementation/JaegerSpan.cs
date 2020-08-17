// <copyright file="JaegerSpan.cs" company="OpenTelemetry Authors">
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;
using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal readonly struct JaegerSpan : TUnionBase
    {
        public JaegerSpan(
            string peerServiceName,
            long traceIdLow,
            long traceIdHigh,
            long spanId,
            long parentSpanId,
            string operationName,
            int flags,
            long startTime,
            long duration,
            in PooledList<JaegerSpanRef> references,
            in PooledList<JaegerTag> tags,
            in PooledList<JaegerLog> logs)
        {
            this.PeerServiceName = peerServiceName;
            this.TraceIdLow = traceIdLow;
            this.TraceIdHigh = traceIdHigh;
            this.SpanId = spanId;
            this.ParentSpanId = parentSpanId;
            this.OperationName = operationName;
            this.Flags = flags;
            this.StartTime = startTime;
            this.Duration = duration;
            this.References = references;
            this.Tags = tags;
            this.Logs = logs;
        }

        public string PeerServiceName { get; }

        public long TraceIdLow { get; }

        public long TraceIdHigh { get; }

        public long SpanId { get; }

        public long ParentSpanId { get; }

        public string OperationName { get; }

        public PooledList<JaegerSpanRef> References { get; }

        public int Flags { get; }

        public long StartTime { get; }

        public long Duration { get; }

        public PooledList<JaegerTag> Tags { get; }

        public PooledList<JaegerLog> Logs { get; }

        public async Task WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("Span");
                await oprot.WriteStructBeginAsync(struc, cancellationToken).ConfigureAwait(false);

                var field = new TField
                {
                    Name = "traceIdLow",
                    Type = TType.I64,
                    ID = 1,
                };

                await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                await oprot.WriteI64Async(this.TraceIdLow, cancellationToken).ConfigureAwait(false);
                await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);

                field.Name = "traceIdHigh";
                field.Type = TType.I64;
                field.ID = 2;

                await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                await oprot.WriteI64Async(this.TraceIdHigh, cancellationToken).ConfigureAwait(false);
                await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);

                field.Name = "spanId";
                field.Type = TType.I64;
                field.ID = 3;

                await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                await oprot.WriteI64Async(this.SpanId, cancellationToken).ConfigureAwait(false);
                await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);

                field.Name = "parentSpanId";
                field.Type = TType.I64;
                field.ID = 4;

                await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                await oprot.WriteI64Async(this.ParentSpanId, cancellationToken).ConfigureAwait(false);
                await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);

                field.Name = "operationName";
                field.Type = TType.String;
                field.ID = 5;

                await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                await oprot.WriteStringAsync(this.OperationName, cancellationToken).ConfigureAwait(false);
                await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);

                if (!this.References.IsEmpty)
                {
                    field.Name = "references";
                    field.Type = TType.List;
                    field.ID = 6;
                    await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.References.Count), cancellationToken).ConfigureAwait(false);

                        for (int i = 0; i < this.References.Count; i++)
                        {
                            await this.References[i].WriteAsync(oprot, cancellationToken).ConfigureAwait(false);
                        }

                        await oprot.WriteListEndAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);
                }

                field.Name = "flags";
                field.Type = TType.I32;
                field.ID = 7;

                await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                await oprot.WriteI32Async(this.Flags, cancellationToken).ConfigureAwait(false);
                await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);

                field.Name = "startTime";
                field.Type = TType.I64;
                field.ID = 8;

                await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                await oprot.WriteI64Async(this.StartTime, cancellationToken).ConfigureAwait(false);
                await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);

                field.Name = "duration";
                field.Type = TType.I64;
                field.ID = 9;

                await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                await oprot.WriteI64Async(this.Duration, cancellationToken).ConfigureAwait(false);
                await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);

                if (!this.Tags.IsEmpty)
                {
                    field.Name = "JaegerTags";
                    field.Type = TType.List;
                    field.ID = 10;

                    await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.Tags.Count), cancellationToken).ConfigureAwait(false);

                        for (int i = 0; i < this.Tags.Count; i++)
                        {
                            await this.Tags[i].WriteAsync(oprot, cancellationToken).ConfigureAwait(false);
                        }

                        await oprot.WriteListEndAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);
                }

                if (!this.Logs.IsEmpty)
                {
                    field.Name = "logs";
                    field.Type = TType.List;
                    field.ID = 11;
                    await oprot.WriteFieldBeginAsync(field, cancellationToken).ConfigureAwait(false);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.Logs.Count), cancellationToken).ConfigureAwait(false);

                        for (int i = 0; i < this.Logs.Count; i++)
                        {
                            await this.Logs[i].WriteAsync(oprot, cancellationToken).ConfigureAwait(false);
                        }

                        await oprot.WriteListEndAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await oprot.WriteFieldEndAsync(cancellationToken).ConfigureAwait(false);
                }

                await oprot.WriteFieldStopAsync(cancellationToken).ConfigureAwait(false);
                await oprot.WriteStructEndAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                oprot.DecrementRecursionDepth();
            }
        }

        public void Return()
        {
            this.References.Return();
            this.Tags.Return();
            if (!this.Logs.IsEmpty)
            {
                for (int i = 0; i < this.Logs.Count; i++)
                {
                    this.Logs[i].Fields.Return();
                }

                this.Logs.Return();
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder("Span(");
            sb.Append(", TraceIdLow: ");
            sb.Append(this.TraceIdLow);
            sb.Append(", TraceIdHigh: ");
            sb.Append(this.TraceIdHigh);
            sb.Append(", SpanId: ");
            sb.Append(this.SpanId);
            sb.Append(", ParentSpanId: ");
            sb.Append(this.ParentSpanId);
            sb.Append(", OperationName: ");
            sb.Append(this.OperationName);
            if (!this.References.IsEmpty)
            {
                sb.Append(", References: ");
                sb.Append(this.References);
            }

            sb.Append(", Flags: ");
            sb.Append(this.Flags);
            sb.Append(", StartTime: ");
            sb.Append(this.StartTime);
            sb.Append(", Duration: ");
            sb.Append(this.Duration);
            if (!this.Tags.IsEmpty)
            {
                sb.Append(", JaegerTags: ");
                sb.Append(this.Tags);
            }

            if (!this.Logs.IsEmpty)
            {
                sb.Append(", Logs: ");
                sb.Append(this.Logs);
            }

            sb.Append(')');
            return sb.ToString();
        }
    }
}
