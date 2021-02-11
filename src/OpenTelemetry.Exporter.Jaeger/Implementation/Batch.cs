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

using System;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTelemetry.Internal;
using Thrift.Protocol;
using Thrift.Protocol.Entities;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class Batch
    {
        private PooledList<BufferWriterMemory> spanMessages;

        public Batch(Process process)
        {
            this.Process = process ?? throw new ArgumentNullException(nameof(process));
            this.spanMessages = PooledList<BufferWriterMemory>.Create();
        }

        public Process Process { get; }

        public int Count => this.spanMessages.Count;

        public override string ToString()
        {
            var sb = new StringBuilder("Batch(");
            sb.Append(", Process: ");
            sb.Append(this.Process?.ToString() ?? "<null>");
            sb.Append(", Spans: ");
            sb.Append(this.spanMessages);
            sb.Append(')');
            return sb.ToString();
        }

        internal void Write(TProtocol oprot)
        {
            oprot.IncrementRecursionDepth();
            try
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
                oprot.Transport.Write(this.Process.Message);
                oprot.WriteFieldEnd();

                field.Name = "spans";
                field.Type = TType.List;
                field.ID = 2;

                oprot.WriteFieldBegin(field);
                {
                    oprot.WriteListBegin(new TList(TType.Struct, this.spanMessages.Count));

                    foreach (var s in this.spanMessages)
                    {
                        oprot.Transport.Write(s.BufferWriter.Buffer, s.Offset, s.Count);
                    }

                    oprot.WriteListEnd();
                }

                oprot.WriteFieldEnd();
                oprot.WriteFieldStop();
                oprot.WriteStructEnd();
            }
            finally
            {
                oprot.DecrementRecursionDepth();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Add(BufferWriterMemory spanMessage)
        {
            PooledList<BufferWriterMemory>.Add(ref this.spanMessages, spanMessage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Clear()
        {
            PooledList<BufferWriterMemory>.Clear(ref this.spanMessages);
        }
    }
}
