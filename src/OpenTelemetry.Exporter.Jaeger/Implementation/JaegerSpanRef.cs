// <copyright file="JaegerSpanRef.cs" company="OpenTelemetry Authors">
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
using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal readonly struct JaegerSpanRef : TUnionBase
    {
        public JaegerSpanRef(JaegerSpanRefType refType, long traceIdLow, long traceIdHigh, long spanId)
        {
            this.RefType = refType;
            this.TraceIdLow = traceIdLow;
            this.TraceIdHigh = traceIdHigh;
            this.SpanId = spanId;
        }

        public JaegerSpanRefType RefType { get; }

        public long TraceIdLow { get; }

        public long TraceIdHigh { get; }

        public long SpanId { get; }

        public void Write(TProtocol oprot)
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("SpanRef");
                oprot.WriteStructBegin(struc);

                var field = new TField
                {
                    Name = "refType",
                    Type = TType.I32,
                    ID = 1,
                };

                oprot.WriteFieldBegin(field);
                oprot.WriteI32((int)this.RefType);
                oprot.WriteFieldEnd();

                field.Name = "traceIdLow";
                field.Type = TType.I64;
                field.ID = 2;

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.TraceIdLow);
                oprot.WriteFieldEnd();

                field.Name = "traceIdHigh";
                field.Type = TType.I64;
                field.ID = 3;

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.TraceIdHigh);
                oprot.WriteFieldEnd();

                field.Name = "spanId";
                field.Type = TType.I64;
                field.ID = 4;

                oprot.WriteFieldBegin(field);
                oprot.WriteI64(this.SpanId);
                oprot.WriteFieldEnd();
                oprot.WriteFieldStop();
                oprot.WriteStructEnd();
            }
            finally
            {
                oprot.DecrementRecursionDepth();
            }
        }

        /// <summary>
        /// <seealso cref="JaegerSpanRefType"/>
        /// </summary>
        /// <returns>A string representation of the object.</returns>
        public override string ToString()
        {
            var sb = new StringBuilder("SpanRef(");
            sb.Append(", RefType: ");
            sb.Append(this.RefType);
            sb.Append(", TraceIdLow: ");
            sb.Append(this.TraceIdLow);
            sb.Append(", TraceIdHigh: ");
            sb.Append(this.TraceIdHigh);
            sb.Append(", SpanId: ");
            sb.Append(this.SpanId);
            sb.Append(')');
            return sb.ToString();
        }
    }
}
