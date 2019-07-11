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

#if NET46
    using Thrift.Transport;
    using TClientTransport = Thrift.Transport.TTransport;
#else
    using Thrift.Transports;
#endif

    internal class InMemoryTransport : TClientTransport
    {
        private readonly MemoryStream byteStream;
        private bool isDisposed;

        public InMemoryTransport()
        {
            this.byteStream = new MemoryStream();
        }

        public override bool IsOpen => true;

#if NET46

        public override void Open()
        {
            // do nothing;
        }

#else

        public override async Task OpenAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await Task.FromCanceled(cancellationToken);
            }
        }

#endif

        public override void Close()
        {
            // do nothing
        }

#if NET46
        public override int Read(
            byte[] buffer,
            int offset,
            int length)
        {
            return this.byteStream.Read(buffer, offset, length);
        }

        public override void Write(byte[] buffer)
        {
            this.byteStream.Write(buffer, 0, buffer.Length);
        }

        public override void Write(byte[] buffer, int offset, int length)
        {
            this.byteStream.Write(buffer, offset, length);
        }

        public override void Flush()
        {
            // do nothing
        }

#else

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
#endif

        public byte[] GetBuffer()
        {
            return this.byteStream.ToArray();
        }

        public void Reset()
        {
            this.byteStream.SetLength(0);
        }

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
