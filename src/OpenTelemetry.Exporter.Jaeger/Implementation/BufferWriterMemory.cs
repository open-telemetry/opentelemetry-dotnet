// <copyright file="BufferWriterMemory.cs" company="OpenTelemetry Authors">
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
using System;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal readonly struct BufferWriterMemory
    {
        public BufferWriterMemory(BufferWriter bufferWriter, int offset, int count)
        {
            this.BufferWriter = bufferWriter;
            this.Offset = offset;
            this.Count = count;
        }

        public BufferWriter BufferWriter { get; }

        public int Offset { get; }

        public int Count { get; }

        public BufferWriterMemory Expand(int length)
        {
            return new BufferWriterMemory(this.BufferWriter, this.Offset, this.Count + length);
        }

        public byte[] ToArray()
        {
            var array = new byte[this.Count];
            Array.Copy(this.BufferWriter.Buffer, this.Offset, array, 0, this.Count);
            return array;
        }
    }
}
