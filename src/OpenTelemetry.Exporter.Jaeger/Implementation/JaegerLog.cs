// <copyright file="JaegerLog.cs" company="OpenTelemetry Authors">
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
    internal readonly struct JaegerLog : TUnionBase
    {
        public JaegerLog(long timestamp, in PooledList<JaegerTag> fields)
        {
            this.Timestamp = timestamp;
            this.Fields = fields;
        }

        public long Timestamp { get; }

        public PooledList<JaegerTag> Fields { get; }

        public async Task WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("Log");
                await oprot.WriteStructBeginAsync(struc, cancellationToken);

                var field = new TField
                {
                    Name = "timestamp",
                    Type = TType.I64,
                    ID = 1,
                };

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await oprot.WriteI64Async(this.Timestamp, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                field.Name = "fields";
                field.Type = TType.List;
                field.ID = 2;

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                {
                    await oprot.WriteListBeginAsync(new TList(TType.Struct, this.Fields.Count), cancellationToken);

                    for (int i = 0; i < this.Fields.Count; i++)
                    {
                        await this.Fields[i].WriteAsync(oprot, cancellationToken);
                    }

                    await oprot.WriteListEndAsync(cancellationToken);
                }

                await oprot.WriteFieldEndAsync(cancellationToken);
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
            var sb = new StringBuilder("Log(");
            sb.Append(", Timestamp: ");
            sb.Append(this.Timestamp);
            sb.Append(", Fields: ");
            sb.Append(this.Fields);
            sb.Append(")");
            return sb.ToString();
        }
    }
}
