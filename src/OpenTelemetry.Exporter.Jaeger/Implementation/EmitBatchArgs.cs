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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class EmitBatchArgs
    {
        public static async Task WriteAsync(int seqId, byte[] processMessage, List<BufferWriterMemory> spanMessages, TProtocol oprot, CancellationToken cancellationToken)
        {
            await oprot.WriteMessageBeginAsync(new TMessage("emitBatch", TMessageType.Oneway, seqId), cancellationToken);

            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("emitBatch_args");
                await oprot.WriteStructBeginAsync(struc, cancellationToken);

                var field = new TField
                {
                    Name = "batch",
                    Type = TType.Struct,
                    ID = 1,
                };

                await oprot.WriteFieldBeginAsync(field, cancellationToken);
                await Batch.WriteAsync(processMessage, spanMessages, oprot, cancellationToken);
                await oprot.WriteFieldEndAsync(cancellationToken);

                await oprot.WriteFieldStopAsync(cancellationToken);
                await oprot.WriteStructEndAsync(cancellationToken);
            }
            finally
            {
                oprot.DecrementRecursionDepth();
            }

            await oprot.WriteMessageEndAsync(cancellationToken);
            await oprot.Transport.FlushAsync(cancellationToken);
        }
    }
}
