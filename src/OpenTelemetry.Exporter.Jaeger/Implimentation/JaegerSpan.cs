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

namespace OpenTelemetry.Exporter.Jaeger.Implimentation
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

#if NET46
    using Thrift.Protocol;
#else
    using Thrift.Protocols;
    using Thrift.Protocols.Entities;
#endif

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

        public List<JaegerSpanRef> References { get; set; }

        public int Flags { get; set; }

        public long StartTime { get; set; }

        public long Duration { get; set; }

        public List<JaegerTag> JaegerTags { get; set; }

        public List<JaegerLog> Logs { get; set; }

#if NET46
        public void Write(TProtocol oprot)
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("Span");
                oprot.WriteStructBegin(struc);

                var field = new TField
                {
                    Name = "traceIdLow",
                    Type = TType.I64,
                    ID = 1,
                };

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.TraceIdLow);
                oprot.WriteFieldEnd();

                field.Name = "traceIdHigh";
                field.Type = TType.I64;
                field.ID = 2;

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.TraceIdHigh);
                oprot.WriteFieldEnd();

                field.Name = "spanId";
                field.Type = TType.I64;
                field.ID = 3;

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.SpanId);
                oprot.WriteFieldEnd();

                field.Name = "parentSpanId";
                field.Type = TType.I64;
                field.ID = 4;

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.ParentSpanId);
                oprot.WriteFieldEnd();

                field.Name = "operationName";
                field.Type = TType.String;
                field.ID = 5;

                oprot.WriteFieldBegin(field);
                oprot.WriteString(this.OperationName);
                oprot.WriteFieldEnd();

                if (this.References != null)
                {
                    field.Name = "references";
                    field.Type = TType.List;
                    field.ID = 6;
                    oprot.WriteFieldBegin(field);
                    {
                        oprot.WriteListBegin(new TList(TType.Struct, this.References.Count));

                        foreach (JaegerSpanRef sr in this.References)
                        {
                            sr.Write(oprot);
                        }

                        oprot.WriteListEnd();
                    }

                    oprot.WriteFieldEnd();
                }

                field.Name = "flags";
                field.Type = TType.I32;
                field.ID = 7;

                oprot.WriteFieldBegin(field);
                oprot.WriteI32(this.Flags);
                oprot.WriteFieldEnd();

                field.Name = "startTime";
                field.Type = TType.I64;
                field.ID = 8;

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.StartTime);
                oprot.WriteFieldEnd();

                field.Name = "duration";
                field.Type = TType.I64;
                field.ID = 9;

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.Duration);
                oprot.WriteFieldEnd();

                if (this.JaegerTags != null)
                {
                    field.Name = "JaegerTags";
                    field.Type = TType.List;
                    field.ID = 10;

                    oprot.WriteFieldBegin(field);
                    {
                        oprot.WriteListBegin(new TList(TType.Struct, this.JaegerTags.Count));

                        foreach (JaegerTag jt in this.JaegerTags)
                        {
                            jt.Write(oprot);
                        }

                        oprot.WriteListEnd();
                    }

                    oprot.WriteFieldEnd();
                }

                if (this.Logs != null)
                {
                    field.Name = "logs";
                    field.Type = TType.List;
                    field.ID = 11;
                    oprot.WriteFieldBegin(field);
                    {
                        oprot.WriteListBegin(new TList(TType.Struct, this.Logs.Count));

                        foreach (JaegerLog jl in this.Logs)
                        {
                            jl.Write(oprot);
                        }

                        oprot.WriteListEnd();
                    }

                    oprot.WriteFieldEnd();
                }

                oprot.WriteFieldStop();
                oprot.WriteStructEnd();
            }
            finally
            {
                oprot.DecrementRecursionDepth();
            }
        }
#else
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

                if (this.References != null)
                {
                    field.Name = "references";
                    field.Type = TType.List;
                    field.ID = 6;
                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.References.Count), cancellationToken);

                        foreach (JaegerSpanRef sr in this.References)
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

                if (this.JaegerTags != null)
                {
                    field.Name = "JaegerTags";
                    field.Type = TType.List;
                    field.ID = 10;

                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    {
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.JaegerTags.Count), cancellationToken);

                        foreach (JaegerTag jt in this.JaegerTags)
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
                        await oprot.WriteListBeginAsync(new TList(TType.Struct, this.Logs.Count), cancellationToken);

                        foreach (JaegerLog jl in this.Logs)
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
#endif

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
            if (this.JaegerTags != null)
            {
                sb.Append(", JaegerTags: ");
                sb.Append(this.JaegerTags);
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
