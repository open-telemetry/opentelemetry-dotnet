// <copyright file="EmitBatchArgs.cs" company="OpenTelemetry Authors">
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
    public class EmitBatchArgs : TAbstractBase
    {
        public EmitBatchArgs()
        {
        }

        public Batch Batch { get; set; }

        public async Task WriteAsync(TProtocol oprot, CancellationToken cancellationToken)
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("emitBatch_args");
                await oprot.WriteStructBeginAsync(struc, cancellationToken);
                if (this.Batch is Batch batch)
                {
                    var field = new TField
                    {
                        Name = "batch",
                        Type = TType.Struct,
                        ID = 1,
                    };

                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    await batch.WriteAsync(oprot, cancellationToken);
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
            var sb = new StringBuilder("emitBatch_args(");

            if (this.Batch is Batch batch)
            {
                sb.Append("Batch: ");
                sb.Append(batch?.ToString() ?? "<null>");
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
}
