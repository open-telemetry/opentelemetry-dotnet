// <copyright file="JaegerSpan.cs" company="OpenTelemetry Authors">
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocols;
using Thrift.Protocols.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public struct JaegerSpan : TAbstractBase
    {
        public JaegerSpan(
            long traceIdLow,
            long traceIdHigh,
            long spanId,
            long parentSpanId,
            string operationName,
            int flags,
            long startTime,
            long duration,
            JaegerSpanRef[] references = null,
            JaegerTag[] tags = null,
            JaegerLog[] logs = null)
        {
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

        public long TraceIdLow { get; set; }

        public long TraceIdHigh { get; set; }

        public long SpanId { get; set; }

        public long ParentSpanId { get; set; }

        public string OperationName { get; set; }

        public JaegerSpanRef[] References { get; set; }

        public int Flags { get; set; }

        public long StartTime { get; set; }

        public long Duration { get; set; }

        public JaegerTag[] Tags { get; set; }

        public JaegerLog[] Logs { get; set; }

#if NETSTANDARD2_1
        public async ValueTask WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
#else
        public async Task WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
#endif
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("Span");
                await oprot.WriteStructBeginAsync(struc, cancellationToken);

                var field = new TField
                {
                    Name = "traceIdLow",
                    Type = TType.I64,
                    ID = 1,
                };

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteI64Async(this.TraceIdLow, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "traceIdHigh";
                field.Type = TType.I64;
                field.ID = 2;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteI64Async(this.TraceIdHigh, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "spanId";
                field.Type = TType.I64;
                field.ID = 3;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteI64Async(this.SpanId, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "parentSpanId";
                field.Type = TType.I64;
                field.ID = 4;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteI64Async(this.ParentSpanId, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "operationName";
                field.Type = TType.String;
                field.ID = 5;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteStringAsync(this.OperationName, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                if (this.References != null)
                {
                    field.Name = "references";
                    field.Type = TType.List;
                    field.ID = 6;
                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.References.Length), cancellationToken);

                        foreach (var sr in this.References)
                        {
                            await sr.WriteAsync(oprot, cancellationToken);
                        }

                        await oprot.WriteListEndAsync(cancellationToken);
                    }

                    await oprot.WriteFieldEndAsync(cancellationToken);
                }

                field.Name = "flags";
                field.Type = TType.I32;
                field.ID = 7;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteI32Async(this.Flags, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "startTime";
                field.Type = TType.I64;
                field.ID = 8;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteI64Async(this.StartTime, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "duration";
                field.Type = TType.I64;
                field.ID = 9;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteI64Async(this.Duration, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                if (this.Tags != null)
                {
                    field.Name = "JaegerTags";
                    field.Type = TType.List;
                    field.ID = 10;

                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.Tags.Length), cancellationToken);

                        foreach (var jt in this.Tags)
                        {
                            await jt.WriteAsync(oprot, cancellationToken);
                        }

                        await oprot.WriteListEndAsync(cancellationToken);
                    }

                    await oprot.WriteFieldEndAsync(cancellationToken);
                }

                if (this.Logs != null)
                {
                    field.Name = "logs";
                    field.Type = TType.List;
                    field.ID = 11;
                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.Logs.Length), cancellationToken);

                        foreach (var jl in this.Logs)
                        {
                            await jl.WriteAsync(oprot, cancellationToken);
                        }

                        await oprot.WriteListEndAsync(cancellationToken);
                    }

                    await oprot.WriteFieldEndAsync(cancellationToken);
                }

                await oprot.WriteFieldStopAsync(cancellationToken);
                await oprot.WriteStructEndAsync(cancellationToken);
            }
            finally
            {
                oprot.DecrementRecursionDepth();
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
            if (this.References != null)
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
            if (this.Tags != null)
            {
                sb.Append(", JaegerTags: ");
                sb.Append(this.Tags);
            }

            if (this.Logs != null)
            {
                sb.Append(", Logs: ");
                sb.Append(this.Logs);
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
}
