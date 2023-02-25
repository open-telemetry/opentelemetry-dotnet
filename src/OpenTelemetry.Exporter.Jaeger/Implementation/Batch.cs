// <copyright file="Batch.cs" company="OpenTelemetry Authors">
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
    internal sealed class Batch
    {
        public Batch(Process process, TProtocol protocol)
        {
            this.BatchBeginMessage = GenerateBeginMessage(process, protocol, out int spanCountPosition);
            this.SpanCountPosition = spanCountPosition;
            this.BatchEndMessage = GenerateEndMessage(protocol);
        }

        public byte[] BatchBeginMessage { get; }

        public int SpanCountPosition { get; set; }

        public byte[] BatchEndMessage { get; }

        public int MinimumMessageSize => this.BatchBeginMessage.Length
            + this.BatchEndMessage.Length;

        private static byte[] GenerateBeginMessage(Process process, TProtocol oprot, out int spanCountPosition)
        {
            var struc = new TStruct("Batch");

            oprot.WriteStructBegin(struc);

            var field = new TField
            {
                Name = "process",
                Type = TType.Struct,
                ID = 1,
            };

            oprot.WriteFieldBegin(field);
            process.Write(oprot);
            oprot.WriteFieldEnd();

            field.Name = "spans";
            field.Type = TType.List;
            field.ID = 2;

            oprot.WriteFieldBegin(field);

            oprot.WriteListBegin(new TList(TType.Struct, 0), out spanCountPosition);

            byte[] beginMessage = oprot.WrittenData.ToArray();
            oprot.Clear();
            return beginMessage;
        }

        private static byte[] GenerateEndMessage(TProtocol oprot)
        {
            oprot.WriteListEnd();

            oprot.WriteFieldEnd();
            oprot.WriteFieldStop();
            oprot.WriteStructEnd();

            byte[] endMessage = oprot.WrittenData.ToArray();
            oprot.Clear();
            return endMessage;
        }
    }
}
