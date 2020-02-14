﻿// <copyright file="PooledByteBufferWriter.cs" company="OpenTelemetry Authors">
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
using System.Buffers;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    // Adapted from: https://github.com/dotnet/runtime/blob/81bf79fd9aa75305e55abe2f7e9ef3f60624a3a1/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/PooledByteBufferWriter.cs
    internal sealed class PooledByteBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private const int MinimumBufferSize = 256;
        private readonly int initialCapacity;
        private byte[] rentedBuffer;

        public PooledByteBufferWriter(int initialCapacity)
        {
            this.initialCapacity = initialCapacity;
            this.Initialize();
        }

        public ReadOnlyMemory<byte> WrittenMemory => this.rentedBuffer.AsMemory(0, this.WrittenCount);

        public int WrittenCount { get; private set; }

        public int Capacity => this.rentedBuffer.Length;

        public int FreeCapacity => this.rentedBuffer.Length - this.WrittenCount;

        public ArraySegment<byte> SwapOutBuffer()
        {
            var buffer = new ArraySegment<byte>(this.rentedBuffer, 0, this.WrittenCount);
            this.Initialize();
            return buffer;
        }

        public void Clear()
        {
            this.WrittenCount = 0;
        }

        // Returns the rented buffer back to the pool
        public void Dispose()
        {
            if (this.rentedBuffer == null)
            {
                return;
            }

            ArrayPool<byte>.Shared.Return(this.rentedBuffer);
            this.rentedBuffer = null;
        }

        public void Advance(int count)
        {
            this.WrittenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            this.CheckAndResizeBuffer(sizeHint);
            return this.rentedBuffer.AsMemory(this.WrittenCount);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            this.CheckAndResizeBuffer(sizeHint);
            return this.rentedBuffer.AsSpan(this.WrittenCount);
        }

        private void Initialize()
        {
            this.rentedBuffer = ArrayPool<byte>.Shared.Rent(this.initialCapacity);
            this.Clear();
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            if (sizeHint <= 0)
            {
                sizeHint = MinimumBufferSize;
            }

            int availableSpace = this.rentedBuffer.Length - this.WrittenCount;

            if (sizeHint > availableSpace)
            {
                int growBy = Math.Max(sizeHint, this.rentedBuffer.Length);

                int newSize = checked(this.rentedBuffer.Length + growBy);

                byte[] oldBuffer = this.rentedBuffer;

                this.rentedBuffer = ArrayPool<byte>.Shared.Rent(newSize);

                var previousBuffer = oldBuffer.AsSpan(0, this.WrittenCount);
                previousBuffer.CopyTo(this.rentedBuffer);
                ArrayPool<byte>.Shared.Return(oldBuffer);
            }
        }
    }
}
