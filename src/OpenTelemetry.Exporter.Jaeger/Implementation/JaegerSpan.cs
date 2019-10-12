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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocols;
using Thrift.Protocols.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public class JaegerSpan : TAbstractBase
    {
        public JaegerSpan()
        {
        }

        public JaegerSpan(long traceIdLow, long traceIdHigh, long spanId, long parentSpanId, string operationName, int flags, long startTime, long duration)
            : this()
        {
            this.TraceIdLow = traceIdLow;
            this.TraceIdHigh = traceIdHigh;
            this.SpanId = spanId;
            this.ParentSpanId = parentSpanId;
            this.OperationName = operationName;
            this.Flags = flags;
            this.StartTime = startTime;
            this.Duration = duration;
        }

        public long TraceIdLow { get; set; }

        public long TraceIdHigh { get; set; }

        public long SpanId { get; set; }

        public long ParentSpanId { get; set; }

        public string OperationName { get; set; }

        public IEnumerable<JaegerSpanRef> References { get; set; }

        public int Flags { get; set; }

        public long StartTime { get; set; }

        public long Duration { get; set; }

        public IEnumerable<JaegerTag> JaegerTags { get; set; }

        public IEnumerable<JaegerLog> Logs { get; set; }

        public async Task WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
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

                if (this.References is IEnumerable<JaegerSpanRef> references)
                {
                    field.Name = "references";
                    field.Type = TType.List;
                    field.ID = 6;
                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, references.Count()), cancellationToken);

                        foreach (JaegerSpanRef sr in references)
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

                if (this.JaegerTags is IEnumerable<JaegerTag> jaegerTags)
                {
                    field.Name = "JaegerTags";
                    field.Type = TType.List;
                    field.ID = 10;

                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, jaegerTags.Count()), cancellationToken);

                        foreach (JaegerTag jt in jaegerTags)
                        {
                            await jt.WriteAsync(oprot, cancellationToken);
                        }

                        await oprot.WriteListEndAsync(cancellationToken);
                    }

                    await oprot.WriteFieldEndAsync(cancellationToken);
                }

                if (this.Logs is IEnumerable<JaegerLog> logs)
                {
                    field.Name = "logs";
                    field.Type = TType.List;
                    field.ID = 11;
                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, logs.Count()), cancellationToken);

                        foreach (JaegerLog jl in logs)
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
            if (this.References is IEnumerable<JaegerSpanRef> jaegerSpanRef)
            {
                sb.Append(", References: ");
                sb.Append(jaegerSpanRef);
            }

            sb.Append(", Flags: ");
            sb.Append(this.Flags);
            sb.Append(", StartTime: ");
            sb.Append(this.StartTime);
            sb.Append(", Duration: ");
            sb.Append(this.Duration);
            if (this.JaegerTags is IEnumerable<JaegerTag> tags)
            {
                sb.Append(", JaegerTags: ");
                sb.Append(tags);
            }

            if (this.Logs is IEnumerable<JaegerLog> logs)
            {
                sb.Append(", Logs: ");
                sb.Append(logs);
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
}
