// <copyright file="BufferWriter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal sealed class BufferWriter
    {
        private int index;

        public BufferWriter(int initialCapacity)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "initialCapacity should be non-negative.");
            }

            this.Buffer = new byte[initialCapacity];
        }

        public byte[] Buffer { get; private set; }

        public BufferWriterMemory GetMemory(int length)
        {
            this.CheckAndResizeBuffer(length);
            var memory = new BufferWriterMemory(this, this.index, length);
            this.index += length;
            return memory;
        }

        public void Clear() => this.index = 0;

        private void CheckAndResizeBuffer(int length)
        {
            int availableSpace = this.Buffer.Length - this.index;

            if (length > availableSpace)
            {
                int growBy = Math.Max(length, this.Buffer.Length);

                int newSize = checked(this.Buffer.Length + growBy);

                var previousBuffer = this.Buffer.AsSpan(0, this.index);

                this.Buffer = new byte[newSize];

                previousBuffer.CopyTo(this.Buffer);
            }
        }
    }
}
