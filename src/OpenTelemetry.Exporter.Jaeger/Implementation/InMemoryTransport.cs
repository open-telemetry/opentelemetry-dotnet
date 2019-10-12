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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Thrift.Transports;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public class InMemoryTransport : TClientTransport
    {
        private readonly MemoryStream byteStream;
        private bool isDisposed;

        public InMemoryTransport()
        {
            this.byteStream = new MemoryStream();
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
            CancellationToken cancellationToken) => this.byteStream.ReadAsync(buffer, offset, length, cancellationToken);

        public override Task WriteAsync(byte[] buffer, CancellationToken cancellationToken) => this.byteStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);

        public override Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken) => this.byteStream.WriteAsync(buffer, offset, length, cancellationToken);

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            return Task.CompletedTask;
        }

        public byte[] GetBuffer() => this.byteStream.ToArray();

        public void Reset() => this.byteStream.SetLength(0);

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
