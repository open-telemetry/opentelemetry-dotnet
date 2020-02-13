// <copyright file="InMemoryTransport.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using Thrift.Transports;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal class InMemoryTransport : TClientTransport
    {
        private readonly int initialCapacity;
        private PooledByteBufferWriter bufferWriter;

        public InMemoryTransport(int initialCapacity = 512)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentException(nameof(initialCapacity));
            }

            this.initialCapacity = initialCapacity;
            this.bufferWriter = new PooledByteBufferWriter(initialCapacity);
        }

        public override bool IsOpen => true;

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            return Task.CompletedTask;
        }

        public override void Close()
        {
            // do nothing
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int length,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            var span = new ReadOnlySpan<byte>(buffer, offset, length);
            span.CopyTo(this.bufferWriter.GetSpan(length));
            return Task.CompletedTask;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            return Task.CompletedTask;
        }

        public byte[] FlushToArray()
        {
            var array = this.bufferWriter.WrittenMemory.ToArray();
            this.bufferWriter.Clear();
            return array;
        }

        public PooledByteBufferWriter SwapOutBuffer()
        {
            var previousBufferWriter = this.bufferWriter;
            this.bufferWriter = new PooledByteBufferWriter(this.initialCapacity);
            return previousBufferWriter;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.bufferWriter.Dispose();
            }
        }
    }
}
