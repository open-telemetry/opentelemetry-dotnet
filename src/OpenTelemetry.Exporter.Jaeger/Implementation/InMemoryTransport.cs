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
        private readonly IBufferWriter<byte> bufferWriter;

        public InMemoryTransport(IBufferWriter<byte> bufferWriter)
        {
            this.bufferWriter = bufferWriter ?? throw new ArgumentNullException(nameof(bufferWriter));
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
            this.bufferWriter.Write(new ReadOnlySpan<byte>(buffer, offset, length));
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

        protected override void Dispose(bool disposing)
        {
        }
    }
}
