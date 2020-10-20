// <copyright file="EmitBatchArgs.cs" company="OpenTelemetry Authors">
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

using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class EmitBatchArgs
    {
        public static void Send(int seqId, Batch batch, TProtocol oprot)
        {
            oprot.WriteMessageBegin(new TMessage("emitBatch", TMessageType.Oneway, seqId));

            oprot.IncrementRecursionDepth();
            try
            {
                var struc = new TStruct("emitBatch_args");
                oprot.WriteStructBegin(struc);

                var field = new TField
                {
                    Name = "batch",
                    Type = TType.Struct,
                    ID = 1,
                };

                oprot.WriteFieldBegin(field);
                batch.Write(oprot);
                oprot.WriteFieldEnd();

                oprot.WriteFieldStop();
                oprot.WriteStructEnd();
            }
            finally
            {
                oprot.DecrementRecursionDepth();
            }

            oprot.WriteMessageEnd();
            oprot.Transport.Flush();
        }
    }
}
