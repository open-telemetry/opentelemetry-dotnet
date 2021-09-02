// <copyright file="InMemoryTransport.cs" company="OpenTelemetry Authors">
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
using Thrift.Transport;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal sealed class InMemoryTransport : TTransport
    {
        private readonly BufferWriter bufferWriter;
        private BufferWriterMemory? buffer;

        public InMemoryTransport(int initialCapacity = 512)
        {
            this.bufferWriter = new BufferWriter(initialCapacity);
        }

        public override bool IsOpen => true;

        public override void Close()
        {
            // do nothing
        }

        public override void Write(byte[] buffer, int offset, int length)
        {
            var memory = this.bufferWriter.GetMemory(length);

            var span = new ReadOnlySpan<byte>(buffer, offset, length);
            span.CopyTo(new Span<byte>(memory.BufferWriter.Buffer, memory.Offset, memory.Count));

            if (!this.buffer.HasValue)
            {
                this.buffer = memory;
            }
            else
            {
                // Resize if we already had a window into the current buffer.
                this.buffer = this.buffer.Value.Expand(memory.Count);
            }
        }

        public override int Flush()
        {
            return 0;
        }

        public byte[] ToArray()
        {
            if (!this.buffer.HasValue)
            {
                return Array.Empty<byte>();
            }

            var buffer = this.buffer.Value.ToArray();
            this.buffer = null;
            return buffer;
        }

        public BufferWriterMemory ToBuffer()
        {
            if (!this.buffer.HasValue)
            {
                return new BufferWriterMemory(this.bufferWriter, 0, 0);
            }

            var buffer = this.buffer.Value;
            this.buffer = null;
            return buffer;
        }

        public void Reset()
        {
            this.buffer = null;
            this.bufferWriter.Clear();
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
