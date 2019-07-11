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

namespace OpenTelemetry.Exporter.Jaeger.Implimentation
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

#if NET46
    using Thrift.Protocol;
#else
    using Thrift.Protocols;
    using Thrift.Protocols.Entities;
#endif

    public class EmitBatchArgs : TAbstractBase
    {
        public EmitBatchArgs()
        {
        }

        public Batch Batch { get; set; }

#if NET46

        public void Write(TProtocol oprot)
        {
            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("emitBatch_args");
                oprot.WriteStructBegin(struc);
                if (this.Batch != null)
                {
                    var field = new TField
                    {
                        Name = "batch",
                        Type = TType.Struct,
                        ID = 1,
                    };

                    oprot.WriteFieldBegin(field);
                    this.Batch.Write(oprot);
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
                var struc = new TStruct("emitBatch_args");
                await oprot.WriteStructBeginAsync(struc, cancellationToken);
                if (this.Batch != null)
                {
                    var field = new TField
                    {
                        Name = "batch",
                        Type = TType.Struct,
                        ID = 1,
                    };

                    await oprot.WriteFieldBeginAsync(field, cancellationToken);
                    await this.Batch.WriteAsync(oprot, cancellationToken);
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
            var sb = new StringBuilder("emitBatch_args(");

            if (this.Batch != null)
            {
                sb.Append("Batch: ");
                sb.Append(this.Batch?.ToString() ?? "<null>");
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
}
