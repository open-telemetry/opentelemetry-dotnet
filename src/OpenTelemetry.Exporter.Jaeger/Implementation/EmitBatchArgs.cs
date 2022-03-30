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

#if NETSTANDARD2_0 || NETFRAMEWORK
using System;
#endif
using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal sealed class EmitBatchArgs
    {
        public EmitBatchArgs(TProtocol protocol)
        {
            this.EmitBatchArgsBeginMessage = this.GenerateBeginMessage(protocol, out int seqIdPosition);
            this.SeqIdPosition = seqIdPosition;
            this.EmitBatchArgsEndMessage = this.GenerateEndMessage(protocol);
        }

        public byte[] EmitBatchArgsBeginMessage { get; }

        public int SeqIdPosition { get; }

        public byte[] EmitBatchArgsEndMessage { get; }

        public int MinimumMessageSize => this.EmitBatchArgsBeginMessage.Length
            + this.EmitBatchArgsEndMessage.Length;

        private byte[] GenerateBeginMessage(TProtocol oprot, out int seqIdPosition)
        {
            oprot.WriteMessageBegin(new TMessage("emitBatch", TMessageType.Oneway, 0), out seqIdPosition);

            var struc = new TStruct("emitBatch_args");
            oprot.WriteStructBegin(struc);

            var field = new TField
            {
                Name = "batch",
                Type = TType.Struct,
                ID = 1,
            };

            oprot.WriteFieldBegin(field);

            byte[] beginMessage = oprot.WrittenData.ToArray();
            oprot.Clear();
            return beginMessage;
        }

        private byte[] GenerateEndMessage(TProtocol oprot)
        {
            oprot.WriteFieldEnd();
            oprot.WriteFieldStop();
            oprot.WriteStructEnd();

            oprot.WriteMessageEnd();

            byte[] endMessage = oprot.WrittenData.ToArray();
            oprot.Clear();
            return endMessage;
        }
    }
}
