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

namespace OpenTelemetry.Exporter.Jaeger.Implimentation
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Thrift.Transports;

    internal class InMemoryTransport : TClientTransport
    {
        private readonly MemoryStream byteStream;
        private bool isDisposed;

        public InMemoryTransport()
        {
            this.byteStream = new MemoryStream();
        }

        public override bool IsOpen => true;

        public override async Task OpenAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken);
            }
        }

        public override void Close()
        {
            // do nothing
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int length,
            CancellationToken cancellationToken)
        {
            return await this.byteStream.ReadAsync(buffer, offset, length, cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            await this.byteStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
        {
            await this.byteStream.WriteAsync(buffer, offset, length, cancellationToken);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken);
            }
        }

        public byte[] GetBuffer()
        {
            return this.byteStream.ToArray();
        }

        public void Reset()
        {
            this.byteStream.SetLength(0);
        }

        // IDisposable
        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.byteStream?.Dispose();
                }
            }

            this.isDisposed = true;
        }
    }
}
