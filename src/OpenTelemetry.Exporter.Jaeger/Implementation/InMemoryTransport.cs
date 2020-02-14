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

#if NETSTANDARD2_1
        public override async ValueTask OpenAsync(CancellationToken cancellationToken)
#else
        public override async Task OpenAsync(CancellationToken cancellationToken)
#endif
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public override void Close()
        {
            // do nothing
        }

#if NETSTANDARD2_1
        public override ValueTask<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#else
        public override Task<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#endif
        {
            throw new NotImplementedException();
        }

#if NETSTANDARD2_1
        public override ValueTask WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#else
        public override Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
#endif
        {
            var span = new ReadOnlySpan<byte>(buffer, offset, length);
            span.CopyTo(this.bufferWriter.GetSpan(length));
            this.bufferWriter.Advance(length);
#if NETSTANDARD2_1
            return new ValueTask(Task.CompletedTask);
#else
            return Task.CompletedTask;
#endif
        }

#if NETSTANDARD2_1
        public override async ValueTask FlushAsync(CancellationToken cancellationToken)
#else
        public override async Task FlushAsync(CancellationToken cancellationToken)
#endif
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
            }
        }

        public byte[] FlushToArray()
        {
            var array = this.bufferWriter.WrittenMemory.ToArray();
            this.bufferWriter.Clear();
            return array;
        }

        public ArraySegment<byte> SwapOutBuffer() => this.bufferWriter.SwapOutBuffer();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.bufferWriter.Dispose();
            }
        }
    }
}
